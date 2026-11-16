using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.FaceDetect;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 移动端第三批（对应 Net 版 MobileQueryController 的 scancode / face / service 部分，6 个端点）：
///   scancode/payment 付款码、scancode/pass 通行码、face/upload|status|photo 人脸采集、service/info 客服信息。
/// </summary>
[ApiController]
[Route("api/m")]
public class MobileFaceController : ControllerBase
{
    /// <summary>人脸照片上限：80K（81920 字节）</summary>
    private const int FacePhotoMaxBytes = 80 * 1024;

    private readonly AppDbContext _db;
    private readonly ILogger<MobileFaceController> _logger;

    public MobileFaceController(AppDbContext db, ILogger<MobileFaceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ================= 付款码 / 通行码 =================

    /// <summary>付款码：默认 employee;工号;时间戳（65 秒有效），YKT_PaymentCode=1 时改发一卡通 CRC 码</summary>
    [Authorize]
    [HttpGet("scancode/payment")]
    public IActionResult PaymentCode()
    {
        var p = _db.ST_People.FirstOrDefault(x => x.Person_ID == Pid);
        // 清理本人 1 天前的旧付款码，防止表无限增长
        var cutoff = DateTime.Now.AddDays(-1);
        var stale = _db.SK_ScanCodes.Where(x => x.Person_ID == Pid && x.Get_Time < cutoff).ToList();
        foreach (var oldCode in stale) _db.SK_ScanCodes.Remove(oldCode);

        string code = BuildScanCode(p);

        _db.SK_ScanCodes.Add(new SK_ScanCode
        {
            Moc_No = "WXH5",
            Get_Time = DateTime.Now,
            Person_ID = Pid,
            Person_No = p?.Person_No,
            QR_Code = code,
            State = 0,
            pay_type = 0
        });
        _db.SaveChanges();
        return Ok(new { code, expireSeconds = 60 });
    }

    /// <summary>
    /// 通行码：内容 'employee;工号;过期时间戳'，码内有效期 65 秒，前端按 expireSeconds=60 刷新。
    /// 已挂失的卡不再签发（ST_Card.Card_State ≠ 0），返回 success=false 由前端提示；
    /// 没有卡记录时保持旧行为放行（个别现场未建 ST_Card）。
    /// </summary>
    [Authorize]
    [HttpGet("scancode/pass")]
    public IActionResult PassCode()
    {
        var p = _db.ST_People.FirstOrDefault(x => x.Person_ID == Pid);
        if (p == null) return Ok(new { success = false, code = "", msg = "人员信息不存在" });

        var card = _db.ST_Cards.FirstOrDefault(x => x.Person_ID == Pid);
        if (card != null && (card.Card_State ?? (byte)0) != 0)
            return Ok(new { success = false, code = "", msg = "账户已挂失，请先解除挂失后再使用通行码" });

        return Ok(new { success = true, code = BuildScanCode(p), expireSeconds = 60 });
    }

    private static string BuildScanCode(ST_Person? p)
    {
        string code = "employee;" + (p?.Person_No ?? "") + ";" + GetUnix(DateTime.Now.AddSeconds(65));
        if (AppConfig.YktPaymentCode)
        {
            DateTime bgn = DateTime.Now.AddSeconds(-65);
            DateTime end = DateTime.Now.AddSeconds(65);
            uint cardNo = 0;
            if (!string.IsNullOrEmpty(p?.Card_No)) uint.TryParse(p!.Card_No, out cardNo);
            code = JOCat.QrCode(cardNo, bgn, end);   // 一卡通原生二维码
        }
        return code;
    }

    /// <summary>本地时间转 Unix 秒（与 Net 版 GetUnix 口径一致）</summary>
    public static long GetUnix(DateTime time)
    {
        DateTime startDt = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
        return (long)(time - startDt).TotalSeconds;
    }

    // ================= 人脸采集 =================

    /// <summary>
    /// 人脸采集：上传后压缩到 80K 以下写入 ST_PersonPhoto（无记录新增、有记录覆盖 Photo/id_photo/update_time）。
    /// 返回 { success, approved, size }；未通过返回 { success:false, approved:false, reason }。
    ///
    /// 审核项与 Net 版一致：分辨率 / 亮度 / 内容方差 / 是否检测到人脸 / 人脸朝向是否正立。
    /// </summary>
    [Authorize]
    [HttpPost("face/upload")]
    public IActionResult FaceUpload([FromBody] FaceUploadReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.photo)) return BadRequest("缺少照片");

        byte[] raw;
        try
        {
            string base64 = req.photo;
            int idx = base64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            raw = Convert.FromBase64String(idx >= 0 ? base64.Substring(idx + 7) : base64);
            // 大于 80K 时压缩到 80K 以下（未超限的 JPEG 原样保存）
            raw = CompressUnder80K(raw);
        }
        catch { return Ok(new { success = false, approved = false, reason = "照片格式不正确，无法解析" }); }

        if (!FaceReview(raw, out var reason))
            return Ok(new { success = false, approved = false, reason });

        var fp = _db.ST_PersonPhotos.FirstOrDefault(x => x.Person_ID == Pid);
        if (fp == null) { fp = new ST_PersonPhoto { Person_ID = Pid }; _db.ST_PersonPhotos.Add(fp); }
        fp.Photo = raw;
        fp.id_photo = raw;
        fp.update_time = DateTime.Now;
        _db.SaveChanges();

        return Ok(new { success = true, approved = true, size = raw.Length });
    }

    /// <summary>
    /// 自动审核：过小/无法解析/分辨率低/过暗过曝/内容空白/未检测到人脸/人脸朝向不合格 直接驳回。
    /// 人脸与姿态判定使用移植过来的 PicoDetector（pico 级联，与 H5/小程序同一套参数与阈值）。
    /// </summary>
    private bool FaceReview(byte[] img, out string reason)
    {
        reason = "";
        if (img == null || img.Length < 2 * 1024) { reason = "照片过小或不是有效图片"; return false; }
        try
        {
            using var ms = new MemoryStream(img);
            using var bmp = new Bitmap(ms);
            if (bmp.Width < 200 || bmp.Height < 200) { reason = "分辨率过低（需≥200×200）"; return false; }

            using (var small = new Bitmap(bmp, 32, 32))
            {
                double sum = 0;
                var vals = new double[1024];
                int i = 0;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        double v = small.GetPixel(x, y).GetBrightness();
                        vals[i++] = v; sum += v;
                    }
                double avg = sum / 1024, sq = 0;
                foreach (var v in vals) sq += (v - avg) * (v - avg);
                double std = Math.Sqrt(sq / 1024);
                if (avg < 0.15 || avg > 0.92) { reason = "光线过暗或过曝，请重新拍摄"; return false; }
                if (std < 0.02) { reason = "照片内容无效（可能为空白/纯色图）"; return false; }
            }

            // 核心判定：照片中必须能检测到人脸（pico 级联，与 H5/小程序端同一套参数）。
            // 姿态判定把图按候选朝向旋转后复检：任一朝向检出即视为「有人脸」；
            // 若最优朝向不是 0°（横向 ±90° / 倒置 180° / 偏转超过 20°）则驳回。
            var pose = PicoDetector.Pose(bmp);
            if (!PicoDetector.HasFace(bmp) && !pose.Detected)
            {
                reason = "未检测到人脸，请确保面部正对镜头、光线充足、无遮挡后重新拍摄";
                return false;
            }
            if (pose.Detected && !pose.Ok)
            {
                reason = pose.Pose == "sideways"
                    ? PicoDetector.PoseReasonSideways
                    : pose.Pose == "upside-down"
                        ? PicoDetector.PoseReasonUpsideDown
                        : PicoDetector.PoseReasonTilted;
                return false;
            }
        }
        catch { reason = "无法解析图片，请上传JPG/PNG格式"; return false; }
        return true;
    }

    /// <summary>
    /// 压缩到 80K 以下：先按长边逐级缩小，再逐级降低 JPEG 质量，直至体积达标。
    /// </summary>
    private static byte[] CompressUnder80K(byte[] raw)
    {
        if (raw == null || raw.Length == 0) return raw!;
        if (raw.Length <= FacePhotoMaxBytes && IsJpeg(raw)) return raw;
        try
        {
            using var ms = new MemoryStream(raw);
            using var bmp = new Bitmap(ms);
            ApplyExifOrientation(bmp);   // 关键：先把方向应用到像素

            int[] longSides = { 1080, 720, 480, 320, 240 };
            foreach (int side in longSides)
            {
                byte[] c = EncodeJpegScaled(bmp, side, 80);
                if (c != null && c.Length < FacePhotoMaxBytes) return c;
            }
            int[] qualities = { 90, 80, 70, 60, 50, 40, 30, 20 };
            foreach (int q in qualities)
            {
                byte[] c = EncodeJpegScaled(bmp, 480, q);
                if (c != null && c.Length < FacePhotoMaxBytes) return c;
            }
            return EncodeJpegScaled(bmp, 240, 15) ?? raw;
        }
        catch { return raw; }
    }

    /// <summary>是否 JPEG（FF D8 FF），未超限的 JPEG 可直接沿用、避免无谓二次压缩</summary>
    private static bool IsJpeg(byte[] raw)
        => raw != null && raw.Length > 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF;

    /// <summary>按长边上限等比缩放并编码 JPEG（质量为 1-100）</summary>
    private static byte[]? EncodeJpegScaled(Bitmap src, int maxLongSide, long quality)
    {
        int longSide = Math.Max(src.Width, src.Height);
        double scale = longSide > maxLongSide ? (double)maxLongSide / longSide : 1.0;
        int nw = Math.Max(1, (int)Math.Round(src.Width * scale));
        int nh = Math.Max(1, (int)Math.Round(src.Height * scale));

        using var bmp = new Bitmap(nw, nh);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.White);   // 透明底转白底，避免 JPEG 黑块
            g.DrawImage(src, 0, 0, nw, nh);
        }

        var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var outMs = new MemoryStream();
        if (codec != null)
        {
            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bmp.Save(outMs, codec, ep);
        }
        else
        {
            bmp.Save(outMs, ImageFormat.Jpeg);
        }
        return outMs.ToArray();
    }

    private static void ApplyExifOrientation(Bitmap bmp)
    {
        const int OrientationId = 0x0112;
        if (!bmp.PropertyIdList.Contains(OrientationId)) return;

        var prop = bmp.GetPropertyItem(OrientationId);
        if (prop?.Value == null || prop.Value.Length < 2) return;

        int orientation = BitConverter.ToUInt16(prop.Value, 0);
        switch (orientation)
        {
            case 2: bmp.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
            case 3: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
            case 4: bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); break;
            case 5: bmp.RotateFlip(RotateFlipType.Rotate90FlipX); break;
            case 6: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
            case 7: bmp.RotateFlip(RotateFlipType.Rotate270FlipX); break;
            case 8: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
        }

        try { bmp.RemovePropertyItem(OrientationId); } catch { /* 忽略 */ }
    }

    /// <summary>人脸采集状态：按 ST_PersonPhoto 是否有记录判定（有记录即已通过），照片以 dataURL 返回便于直接展示</summary>
    [Authorize]
    [HttpGet("face/status")]
    public IActionResult FaceStatus()
    {
        var fp = _db.ST_PersonPhotos.AsNoTracking().FirstOrDefault(x => x.Person_ID == Pid);
        if (fp == null)
            return Ok(new { collected = false, status = "none", hasPhoto = false, photo = "", updateTime = "" });

        // Photo 为主照片，id_photo 为采集时的副本，取有值的一个
        byte[]? bytes = (fp.Photo != null && fp.Photo.Length > 0) ? fp.Photo : fp.id_photo;
        bool hasPhoto = bytes != null && bytes.Length > 0;
        return Ok(new
        {
            collected = true,
            status = "approved",
            hasPhoto,
            photo = hasPhoto ? "data:image/jpeg;base64," + Convert.ToBase64String(bytes!) : "",
            updateTime = fp.update_time.HasValue ? fp.update_time.Value.ToString("yyyy-MM-dd HH:mm") : ""
        });
    }

    /// <summary>读取本人已采集的人脸照片（二进制返回，用法同 canteen/photo/{id}）</summary>
    [Authorize]
    [HttpGet("face/photo")]
    public IActionResult FacePhoto()
    {
        var fp = _db.ST_PersonPhotos.AsNoTracking().FirstOrDefault(x => x.Person_ID == Pid);
        if (fp == null) return NotFound();

        byte[]? bytes = (fp.Photo != null && fp.Photo.Length > 0) ? fp.Photo : fp.id_photo;
        if (bytes == null || bytes.Length == 0) return NotFound();

        Response.Headers.CacheControl = "public,max-age=3600";
        return File(bytes, "image/jpeg");
    }

    // ================= 客服信息 =================

    /// <summary>
    /// 客服信息：ST_SysParam 的 SYS.ServicePhone / SYS.ServiceWindow / SYS.ServiceTime。
    /// 后台未配置时返回空串（前端展示「未配置」），不在代码里写死演示值。
    /// 允许匿名：尚未绑定成功的用户同样需要看到客服电话求助。
    /// </summary>
    [AllowAnonymous]
    [HttpGet("service/info")]
    public IActionResult ServiceInfo()
    {
        string phone = SysParamValue("SYS.ServicePhone");
        return Ok(new
        {
            servicePhone = phone,
            // 拨号用号码：去掉空格/短横线等非数字字符（保留开头的 +）
            servicePhoneTel = TelOf(phone),
            serviceWindow = SysParamValue("SYS.ServiceWindow"),
            serviceTime = SysParamValue("SYS.ServiceTime")
        });
    }

    private string SysParamValue(string code)
    {
        string? v = _db.ST_SysParams.AsNoTracking()
            .Where(s => s.Param_Code == code)
            .Select(s => s.Param_Value).FirstOrDefault();
        return v == null ? "" : v.Trim();
    }

    /// <summary>客服电话转可拨号号码：保留数字与开头的 +</summary>
    private static string TelOf(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in phone)
        {
            if (char.IsDigit(c) || (c == '+' && sb.Length == 0)) sb.Append(c);
        }
        return sb.ToString();
    }
}

public class FaceUploadReq { public string? photo { get; set; } }
