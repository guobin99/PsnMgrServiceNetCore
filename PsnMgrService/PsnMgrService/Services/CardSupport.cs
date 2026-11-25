using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using PsnMgrService.FaceDetect;

namespace PsnMgrService.Services;

/* =====================================================================
 * 卡务中心-人员与组织 公共支撑
 * 依据《人脸通BS系统-卡务中心-人员与组织详细设计文档》
 * 一个文件承载：编号规则、脱敏、照片处理、XLSX 读写、编码字典、DTO
 * ===================================================================== */

/// <summary>人员/部门/编码字段规则，前后端与两套后端共用同一口径。</summary>
public static class CardRules
{
    public static string Trim(string? value) => (value ?? string.Empty).Trim();

    /// <summary>人员编号：2 至 20 位，字母或数字。</summary>
    public static bool IsValidPersonNo(string? value)
    {
        var no = Trim(value);
        if (no.Length < 2 || no.Length > 20) return false;
        return no.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
    }

    public static bool IsValidName(string? value)
    {
        var name = Trim(value);
        return name.Length >= 1 && name.Length <= 20;
    }

    public static bool IsValidMobile(string? value)
    {
        var mobile = Trim(value);
        if (mobile.Length == 0) return true;
        if (mobile.Length != 11) return false;
        if (mobile[0] != '1') return false;
        return mobile.All(c => c >= '0' && c <= '9');
    }

    public static bool IsValidIdNumber(string? value)
    {
        var id = Trim(value);
        if (id.Length == 0) return true;
        if (id.Length != 15 && id.Length != 18) return false;
        return id.All(c => (c >= '0' && c <= '9') || c == 'X' || c == 'x');
    }

    /// <summary>卡号：非空时不超过 12 位；空串表示未发卡，不参与唯一性。</summary>
    public static bool IsValidCardNo(string? value)
    {
        var no = Trim(value);
        return no.Length <= 12;
    }

    /// <summary>部门编号：偶数长度、不小于 4 位（根部门 4 位，逐级两位），两位一级。</summary>
    public static bool IsValidDeptNo(string? value)
    {
        var no = Trim(value);
        if (no.Length < 4 || no.Length % 2 != 0 || no.Length > 30) return false;
        return no.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
    }

    public static string NormalizeDate(string? value)
    {
        var text = Trim(value);
        if (text.Length == 0) return string.Empty;
        DateTime parsed;
        if (DateTime.TryParse(text, out parsed)) return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return string.Empty;
    }

    public static DateTime? ParseDate(string? value)
    {
        var text = Trim(value);
        if (text.Length == 0) return null;
        DateTime parsed;
        return DateTime.TryParse(text, out parsed) ? parsed : (DateTime?)null;
    }

    /// <summary>
    /// 性别归一：现场库 ST_Person.Sex 实际存 '1'（男）/ '2'（女），少量历史行存中文字面。
    /// 统一写回 '1'/'2'，列宽 2 个字符；无法识别时返回空串（等同未填）。
    /// </summary>
    public static string NormalizeSex(string? value)
    {
        var sex = Trim(value);
        if (sex.Length == 0) return string.Empty;
        if (sex == "1" || sex.StartsWith("男")) return "1";
        if (sex == "2" || sex.StartsWith("女")) return "2";
        return string.Empty;
    }

    /// <summary>性别码转显示文案：1→男、2→女，历史中文值原样返回，其余为空。</summary>
    public static string SexText(string? value)
    {
        var sex = Trim(value);
        if (sex.Length == 0) return string.Empty;
        if (sex == "1" || sex.StartsWith("男")) return "男";
        if (sex == "2" || sex.StartsWith("女")) return "女";
        return string.Empty;
    }

    /// <summary>身份证号脱敏：保留前六后四。</summary>
    public static string? MaskIdNumber(string? value)
    {
        var id = Trim(value);
        if (id.Length == 0) return value;
        if (id.Length <= 10) return new string('*', id.Length);
        return id.Substring(0, 6) + new string('*', id.Length - 10) + id.Substring(id.Length - 4);
    }

    /// <summary>手机号脱敏：保留前三后四。</summary>
    public static string? MaskMobile(string? value)
    {
        var mobile = Trim(value);
        if (mobile.Length == 0) return value;
        if (mobile.Length <= 7) return new string('*', mobile.Length);
        return mobile.Substring(0, 3) + new string('*', mobile.Length - 7) + mobile.Substring(mobile.Length - 4);
    }

    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public static int ClampPageSize(int pageSize, int fallback = 20)
    {
        var size = pageSize <= 0 ? fallback : pageSize;
        if (size < 1) return 1;
        return size > 200 ? 200 : size;
    }
}

/// <summary>照片处理：统一 JPEG、4:3、长边不超过 640、单张不超过 100KB。</summary>
public static class CardImage
{
    public const int MaxSourceBytes = 5 * 1024 * 1024;
    public const int MaxOutputBytes = 100 * 1024;
    private const int MaxWidth = 640;
    private const int MaxHeight = 480;

    /// <summary>文件头校验，避免把可执行文件改扩展名后上传。</summary>
    public static bool IsSupported(byte[] raw)
    {
        if (raw == null || raw.Length < 8) return false;
        if (raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF) return true;              // JPEG
        if (raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47) return true; // PNG
        if (raw[0] == 0x42 && raw[1] == 0x4D) return true;                                // BMP（兼容截图）
        return false;
    }

    /// <summary>处理为证件照：居中裁剪 4:3，等比缩小到 640×480 以内，压缩到 100KB 以下。</summary>
    public static byte[] Process(byte[] raw, out int width, out int height)
    {
        width = 0;
        height = 0;
        using var source = Image.FromStream(new MemoryStream(raw), true, true);
        using var normalized = Normalize(source);

        // 居中裁剪出 4:3 区域（不放大原图）
        var cropWidth = normalized.Width;
        var cropHeight = (int)Math.Round(cropWidth * 3.0 / 4.0);
        if (cropHeight > normalized.Height)
        {
            cropHeight = normalized.Height;
            cropWidth = (int)Math.Round(cropHeight * 4.0 / 3.0);
        }
        var offsetX = (normalized.Width - cropWidth) / 2;
        var offsetY = (normalized.Height - cropHeight) / 2;
        using var cropped = new Bitmap(cropWidth, cropHeight);
        using (var g = Graphics.FromImage(cropped))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(normalized, new Rectangle(0, 0, cropWidth, cropHeight),
                new Rectangle(offsetX, offsetY, cropWidth, cropHeight), GraphicsUnit.Pixel);
        }

        var scale = Math.Min(1.0, Math.Min((double)MaxWidth / cropped.Width, (double)MaxHeight / cropped.Height));
        var targetWidth = Math.Max(1, (int)Math.Round(cropped.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(cropped.Height * scale));

        using var resized = new Bitmap(targetWidth, targetHeight);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(cropped, 0, 0, targetWidth, targetHeight);
        }

        var quality = 88;
        var bytes = EncodeJpeg(resized, quality);
        while (bytes.Length > MaxOutputBytes && quality > 40)
        {
            quality -= 8;
            bytes = EncodeJpeg(resized, quality);
        }

        // 极端情况下继续缩小尺寸，保证单张不超过 100KB
        var current = resized;
        while (bytes.Length > MaxOutputBytes && current.Width > 120)
        {
            var next = new Bitmap(Math.Max(1, current.Width * 9 / 10), Math.Max(1, current.Height * 9 / 10));
            using (var g = Graphics.FromImage(next))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(current, 0, 0, next.Width, next.Height);
            }
            if (!ReferenceEquals(current, resized)) current.Dispose();
            current = next;
            bytes = EncodeJpeg(current, 70);
        }

        width = current.Width;
        height = current.Height;
        if (!ReferenceEquals(current, resized)) current.Dispose();
        return bytes;
    }

    /// <summary>缩略图：宽度 160，用于列表与网格。</summary>
    public static byte[] Thumb(byte[] jpeg, int targetWidth = 160)
    {
        using var source = Image.FromStream(new MemoryStream(jpeg), true, true);
        var width = Math.Min(targetWidth, source.Width);
        var height = (int)Math.Round(source.Height * (double)width / source.Width);
        using var thumb = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        using (var g = Graphics.FromImage(thumb))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, thumb.Width, thumb.Height);
        }
        return EncodeJpeg(thumb, 80);
    }

    public static (int width, int height) Measure(byte[] raw)
    {
        try
        {
            using var image = Image.FromStream(new MemoryStream(raw), true, true);
            return (image.Width, image.Height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static Bitmap Normalize(Image source)
    {
        // EXIF 方向校正：只处理 180、顺时针 90、逆时针 90 三种常见值
        var orientation = 1;
        try
        {
            if (source.PropertyIdList.Any(id => id == 0x0112))
            {
                var item = source.GetPropertyItem(0x0112);
                if (item != null && item.Value != null && item.Value.Length > 0) orientation = item.Value[0];
            }
        }
        catch
        {
            orientation = 1;
        }

        var bitmap = new Bitmap(source.Width, source.Height);
        using (var g = Graphics.FromImage(bitmap)) g.DrawImage(source, 0, 0, source.Width, source.Height);

        switch (orientation)
        {
            case 3:
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                break;
            case 6:
                bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                break;
            case 8:
                bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                break;
        }
        return bitmap;
    }

    private static byte[] EncodeJpeg(Image image, int quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
        if (encoder == null)
        {
            using var fallback = new MemoryStream();
            image.Save(fallback, ImageFormat.Jpeg);
            return fallback.ToArray();
        }

        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        using var buffer = new MemoryStream();
        image.Save(buffer, encoder, parameters);
        return buffer.ToArray();
    }
}

/// <summary>极简 XLSX 读写：只依赖 System.IO.Compression，不引入第三方包。</summary>
public static class CardXlsx
{
    private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>生成单工作表 xlsx；首行为表头。</summary>
    public static byte[] Build(string sheetName, IList<string[]> rows, int[]? numberColumns = null)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>");

            WriteEntry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                "<Relationship Id=\"rId1\" Type=\"" + NsRel + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");

            WriteEntry(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"" + NsMain + "\" xmlns:r=\"" + NsRel + "\">" +
                "<sheets><sheet name=\"" + Escape(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                "</workbook>");

            WriteEntry(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"" + NsPkgRel + "\">" +
                "<Relationship Id=\"rId1\" Type=\"" + NsRel + "/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"" + NsRel + "/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");

            WriteEntry(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"" + NsMain + "\">" +
                "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"宋体\"/></font></fonts>" +
                "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                "<borders count=\"1\"><border/></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                "</styleSheet>");

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"").Append(NsMain).Append("\"><sheetData>");
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r] ?? Array.Empty<string>();
                sheet.Append("<row r=\"").Append(r + 1).Append("\">");
                for (var c = 0; c < row.Length; c++)
                {
                    var value = row[c] ?? string.Empty;
                    var reference = ColumnName(c) + (r + 1);
                    if (value.Length == 0) continue;
                    var numeric = numberColumns != null && numberColumns.Contains(c) &&
                                  decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                    if (numeric)
                    {
                        sheet.Append("<c r=\"").Append(reference).Append("\"><v>").Append(Escape(value)).Append("</v></c>");
                    }
                    else
                    {
                        sheet.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                            .Append(Escape(value)).Append("</t></is></c>");
                    }
                }
                sheet.Append("</row>");
            }
            sheet.Append("</sheetData></worksheet>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        return buffer.ToArray();
    }

    /// <summary>读取首个工作表，返回按行组织的字符串数组（列已按最大列数补齐）。</summary>
    public static List<string[]> Read(byte[] file, string fileName)
    {
        var name = (fileName ?? string.Empty).ToLowerInvariant();
        if (name.EndsWith(".csv", StringComparison.Ordinal)) return ReadCsv(file);
        if (!name.EndsWith(".xlsx", StringComparison.Ordinal))
            throw new InvalidOperationException("仅支持 xlsx 或 csv 文件");

        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        var sheetEntry = zip.Entries.FirstOrDefault(e => e.FullName == "xl/worksheets/sheet1.xml")
                         ?? zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null) throw new InvalidOperationException("文件缺少工作表，请使用系统模板");

        var shared = new List<string>();
        var sharedEntry = zip.Entries.FirstOrDefault(e => e.FullName == "xl/sharedStrings.xml");
        if (sharedEntry != null)
        {
            using var stream = sharedEntry.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = NsMain;
            shared.AddRange(doc.Descendants(ns + "si").Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))));
        }

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        XNamespace main = NsMain;

        var result = new List<string[]>();
        foreach (var row in sheetDoc.Descendants(main + "row"))
        {
            var cells = new Dictionary<int, string>();
            var maxIndex = -1;
            foreach (var cell in row.Elements(main + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var index = ColumnIndex(reference);
                if (index < 0) index = maxIndex + 1;
                var type = (string?)cell.Attribute("t");
                string value;
                if (type == "s")
                {
                    var raw = cell.Element(main + "v")?.Value;
                    int sharedIndex;
                    value = int.TryParse(raw, out sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count
                        ? shared[sharedIndex]
                        : string.Empty;
                }
                else if (type == "inlineStr")
                {
                    value = string.Concat(cell.Descendants(main + "t").Select(t => t.Value));
                }
                else
                {
                    value = cell.Element(main + "v")?.Value ?? string.Empty;
                }
                cells[index] = value;
                if (index > maxIndex) maxIndex = index;
            }

            var array = new string[maxIndex + 1];
            for (var i = 0; i < array.Length; i++) array[i] = cells.TryGetValue(i, out var value) ? value : string.Empty;
            if (array.Length > 0) result.Add(array);
        }

        // 去掉尾部完全空行
        while (result.Count > 0 && result[result.Count - 1].All(string.IsNullOrWhiteSpace)) result.RemoveAt(result.Count - 1);
        return result;
    }

    private static List<string[]> ReadCsv(byte[] file)
    {
        var encoding = Encoding.UTF8;
        var text = encoding.GetString(file);
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
        var rows = new List<string[]>();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0) continue;
            rows.Add(line.Split(',').Select(v => v.Trim().Trim('"')).ToArray());
        }
        while (rows.Count > 0 && rows[rows.Count - 1].All(string.IsNullOrWhiteSpace)) rows.RemoveAt(rows.Count - 1);
        return rows;
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Escape(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var current = index + 1;
        while (current > 0)
        {
            var remainder = (current - 1) % 26;
            name = (char)('A' + remainder) + name;
            current = (current - 1) / 26;
        }
        return name;
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var index = 0;
        foreach (var c in reference)
        {
            if (c >= 'A' && c <= 'Z') index = index * 26 + (c - 'A' + 1);
            else if (c >= 'a' && c <= 'z') index = index * 26 + (c - 'a' + 1);
            else break;
        }
        return index - 1;
    }
}

/// <summary>部门台账导出：把组织树与人员统计铺平成一张可报送的表。</summary>
public static class CardDeptExport
{
    /// <summary>导出上限，防止一次拉取全库部门导致内存与响应体过大。</summary>
    public const int Limit = 20000;

    /// <summary>
    /// 生成部门台账行数据。deptId 为空表示导出全部可见部门；
    /// 否则导出该部门（includeChildren 为真时含其下级）。
    /// </summary>
    public static List<string[]> BuildLedger(SqlSession db, int? deptId, bool includeChildren, bool includeDisabled)
    {
        var sql = @"
SELECT d.Dept_ID AS deptId, d.Dept_No AS deptNo, d.Dept_Name AS deptName, d.Dept_Chief AS chief,
       d.Dept_Telphone AS telphone, d.Dept_Duty AS duty, d.BS_Dept_Type AS deptType,
       d.Is_Del AS isDel, d.BS_Sort_No AS sortNo, d.Remark AS remark,
       (SELECT COUNT(1) FROM dbo.ST_Person p
         WHERE p.Dept_ID = d.Dept_ID AND ISNULL(p.Is_Del, 0) = 0) AS activeDirect,
       (SELECT COUNT(1) FROM dbo.ST_Person p
         WHERE p.Dept_ID = d.Dept_ID AND ISNULL(p.Is_Del, 0) = 1) AS deletedCount,
       (SELECT COUNT(1) FROM dbo.ST_Department c
         WHERE ISNULL(c.Is_Del, 0) = 0 AND LEN(c.Dept_No) = LEN(d.Dept_No) + 2
           AND c.Dept_No LIKE d.Dept_No + '%') AS childCount
  FROM dbo.ST_Department d";
        if (!includeDisabled) sql += " WHERE ISNULL(d.Is_Del, 0) = 0";
        sql += " ORDER BY d.Dept_No";

        var rows = db.Query<CardDeptExportRow>(sql);

        // 父部门名称：按编号前缀逐级上溯，缺失时退化为编号。
        var byNo = rows.Where(r => !string.IsNullOrWhiteSpace(r.deptNo))
            .GroupBy(r => r.deptNo!.Trim())
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var row in rows)
        {
            var no = row.deptNo?.Trim() ?? string.Empty;
            row.level = OrgPermissionHelper.GetLevel(no);

            var parentNo = OrgPermissionHelper.GetParentNo(no);
            row.parentNo = parentNo;
            if (parentNo != null && byNo.TryGetValue(parentNo, out var parent))
            {
                row.parentName = parent.deptName;
                row.parentId = parent.deptId;
            }
            else
            {
                row.parentName = null;
            }
        }

        // 含下级在册：每个人员只累加到本部门与所有上级，避免自连接前缀匹配
        foreach (var row in rows)
        {
            row.activeCount = 0;
        }
        foreach (var row in rows)
        {
            row.activeCount += row.activeDirect;
            foreach (var ancestorNo in OrgPermissionHelper.GetAncestorNos(row.deptNo))
            {
                if (byNo.TryGetValue(ancestorNo, out var ancestor)) ancestor.activeCount += row.activeDirect;
            }
        }

        List<CardDeptExportRow> scope;
        if (deptId.HasValue && deptId.Value > 0)
        {
            var root = rows.FirstOrDefault(r => r.deptId == deptId.Value);
            if (root == null) throw new CardDeptExportException("导出部门不存在");
            var prefix = root.deptNo?.Trim() ?? string.Empty;
            scope = includeChildren
                ? rows.Where(r => r.deptId == root.deptId ||
                        (prefix.Length > 0 && (r.deptNo ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)))
                    .ToList()
                : new List<CardDeptExportRow> { root };
        }
        else
        {
            scope = rows.ToList();
        }

        scope = scope.OrderBy(r => r.deptNo ?? string.Empty).ToList();
        if (scope.Count > Limit) throw new CardDeptExportException($"导出上限 {Limit} 行，请缩小范围后重试");

        var table = new List<string[]>
        {
            new[] { "部门编号", "部门名称", "上级部门", "层级", "负责人", "联系电话", "部门类型",
                    "在册人数(含下级)", "本部门在册", "已注销人数", "下级部门数", "状态", "备注" },
        };
        foreach (var row in scope)
        {
            table.Add(new[]
            {
                row.deptNo ?? string.Empty,
                row.deptName ?? string.Empty,
                row.parentName ?? (row.parentNo ?? string.Empty),
                row.level.ToString(CultureInfo.InvariantCulture),
                row.chief ?? string.Empty,
                row.telphone ?? string.Empty,
                row.deptType ?? string.Empty,
                row.activeCount.ToString(CultureInfo.InvariantCulture),
                row.activeDirect.ToString(CultureInfo.InvariantCulture),
                row.deletedCount.ToString(CultureInfo.InvariantCulture),
                row.childCount.ToString(CultureInfo.InvariantCulture),
                row.isDel == true ? "已停用" : "启用中",
                row.remark ?? string.Empty,
            });
        }
        return table;
    }
}

/// <summary>部门台账导出的业务异常：范围不合法或数据量超限。</summary>
public sealed class CardDeptExportException : Exception
{
    public CardDeptExportException(string message) : base(message)
    {
    }
}

/// <summary>编码字典与发号：与 CS 规则保持一致。</summary>
public static class CardLookup
{
    /// <summary>编码表：卡务页面默认展示的 Table_Name 白名单。</summary>
    public static readonly string[] CardTables =
    {
        "BM_NATIONALITY", "BM_DUTY", "BM_UNIT", "BM_KAOQIN", "BM_WAREUNIT",
        "BM_MJDOORAPBNo", "BM_MJDOORHideBackNo", "BM_PERSONTYPE",
    };

    /// <summary>编码表的引用字段映射；返回 null 表示不统计引用数。</summary>
    public static (string Table, string Column)? RefField(string tableName)
    {
        switch ((tableName ?? string.Empty).ToUpperInvariant())
        {
            case "BM_NATIONALITY": return ("ST_Person", "Nationality");
            case "BM_DUTY": return ("ST_Person", "Duty");
            case "BM_PERSONTYPE": return ("ST_Person", "Person_Type");
            default: return null;
        }
    }

    /// <summary>按编码或名称解析编码值；两项都不命中返回 null。</summary>
    public static string? ResolveCode(SqlSession db, string tableName, string? codeOrName)
    {
        var value = CardRules.Trim(codeOrName);
        if (value.Length == 0) return null;
        var row = db.QuerySingle<CardDictItem>(@"
            SELECT TOP 1 Dict_Id AS dictId, Dict_Value AS dictValue
              FROM dbo.ST_Dictionary
             WHERE Table_Name = @table AND (Dict_Id = @value OR Dict_Value = @value)
             ORDER BY CASE WHEN Dict_Id = @value THEN 0 ELSE 1 END", SqlSession.P("@table", tableName), SqlSession.P("@value", value));
        return row?.dictId;
    }

    /// <summary>读取配置项，缺省时返回默认值。</summary>
    public static string ConfigValue(SqlSession db, string key, string fallback)
    {
        var value = db.Scalar("SELECT TOP 1 Config_Value FROM dbo.BS_SysConfig WHERE Config_Key = @key", SqlSession.P("@key", key));
        var text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Trim().Length == 0 ? fallback : text.Trim();
    }

    public static int ConfigInt(SqlSession db, string key, int fallback)
    {
        var text = ConfigValue(db, key, string.Empty);
        int value;
        return int.TryParse(text, out value) ? value : fallback;
    }
}

/// <summary>人员编号：沿用 CS“最长编号尾段加一”的规则，并加事务锁防重号。</summary>
public static class CardPersonNo
{
    private const string IdentityKey = "ST_PersonNo";

    /// <summary>解析库中当前最长编号的前缀与位宽。</summary>
    public static (string prefix, int width, int value) ParseMax(SqlSession db)
    {
        // 取长度最长的一批编号，避免全表扫描；数字尾段越长，编号通常也越长
        var numbers = db.Query<CardNoProbe>(
            "SELECT TOP 2000 Person_No AS personNo FROM dbo.ST_Person ORDER BY LEN(Person_No) DESC, Person_No DESC");

        var bestPrefix = string.Empty;
        var bestWidth = 0;
        long bestValue = 0;
        foreach (var item in numbers)
        {
            var no = item.personNo ?? string.Empty;
            var tail = 0;
            for (var i = no.Length - 1; i >= 0; i--)
            {
                var c = no[i];
                if (c < '0' || c > '9') break;
                tail++;
            }
            if (tail == 0) continue;

            long value;
            if (!long.TryParse(no.Substring(no.Length - tail), out value)) continue;
            if (tail > bestWidth || (tail == bestWidth && value > bestValue))
            {
                bestWidth = tail;
                bestValue = value;
                bestPrefix = no.Substring(0, no.Length - tail);
            }
        }

        return (bestPrefix, bestWidth, (int)Math.Min(int.MaxValue, bestValue));
    }

    /// <summary>预取下一个可用编号（不占号）。</summary>
    public static string Peek(SqlSession db)
    {
        var current = db.Scalar("SELECT Current_ID FROM dbo.ST_Identity WHERE Table_Name = @key", SqlSession.P("@key", IdentityKey));
        var config = Defaults(db);
        if (current == null)
        {
            var max = ParseMax(db);
            var width = max.width > 0 ? max.width : config.width;
            var prefix = max.width > 0 ? max.prefix : config.prefix;
            return Format(prefix, max.value + 1, width);
        }

        var value = Convert.ToInt32(current, CultureInfo.InvariantCulture);
        var probe = ParseMax(db);
        var probeWidth = probe.width > 0 ? probe.width : config.width;
        var probePrefix = probe.width > 0 ? probe.prefix : config.prefix;
        return Format(probePrefix, value, probeWidth);
    }

    /// <summary>在事务内发号：对 ST_Identity 行加 UPDLOCK，格式化后校验未占用。</summary>
    public static string Next(SqlSession db, out string error)
    {
        error = null;
        var config = Defaults(db);
        var probe = ParseMax(db);
        var prefix = probe.width > 0 ? probe.prefix : config.prefix;
        var width = probe.width > 0 ? probe.width : config.width;

        var current = db.Scalar(
            "SELECT Current_ID FROM dbo.ST_Identity WITH (UPDLOCK, ROWLOCK) WHERE Table_Name = @key",
            SqlSession.P("@key", IdentityKey));

        int value;
        if (current == null)
        {
            // 首次使用：按库内最长编号初始化
            value = probe.value + 1;
            if (value < 1) value = 1;
            db.Exec("INSERT INTO dbo.ST_Identity (Table_Name, Current_ID) VALUES (@key, @next)",
                SqlSession.P("@key", IdentityKey), SqlSession.P("@next", value + 1));
        }
        else
        {
            value = Convert.ToInt32(current, CultureInfo.InvariantCulture);
            if (value <= probe.value) value = probe.value + 1;
            db.Exec("UPDATE dbo.ST_Identity SET Current_ID = @next WHERE Table_Name = @key",
                SqlSession.P("@next", value + 1), SqlSession.P("@key", IdentityKey));
        }

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = Format(prefix, value, width);
            var exists = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.ST_Person WHERE Person_No = @no AND ISNULL(Is_Del, 0) = 0",
                SqlSession.P("@no", candidate));
            if (exists == 0) return candidate;

            value++;
            db.Exec("UPDATE dbo.ST_Identity SET Current_ID = @next WHERE Table_Name = @key",
                SqlSession.P("@next", value + 1), SqlSession.P("@key", IdentityKey));
        }

        error = "人员编号生成失败，请重试";
        return null;
    }

    /// <summary>取人员主键：优先调用 CS 存储过程，缺失时回退到 ST_Identity 发号。</summary>
    public static int NextPersonId(SqlSession db)
    {
        try
        {
            // CS 的 st_pd_GetMaxIDEx 只通过 OUTPUT 参数返回，需用批处理取回
            var value = db.Scalar(
                "DECLARE @newId INT; " +
                "EXEC dbo.st_pd_GetMaxIDEx @TableName = @table, @MaxTableID = @newId OUTPUT, @Increment = 1; " +
                "SELECT @newId;", SqlSession.P("@table", "ST_Person"));
            if (value != null) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // 存储过程缺失或参数不匹配时走回退逻辑
        }

        var current = db.Scalar("SELECT Current_ID FROM dbo.ST_Identity WITH (UPDLOCK, ROWLOCK) WHERE Table_Name = @table",
            SqlSession.P("@table", "ST_Person"));
        if (current == null)
        {
            var maxId = db.ScalarInt("SELECT ISNULL(MAX(Person_ID), 0) FROM dbo.ST_Person") + 1;
            db.Exec("INSERT INTO dbo.ST_Identity (Table_Name, Current_ID) VALUES (@table, @next)",
                SqlSession.P("@table", "ST_Person"), SqlSession.P("@next", maxId + 1));
            return maxId;
        }

        var id = Convert.ToInt32(current, CultureInfo.InvariantCulture);
        db.Exec("UPDATE dbo.ST_Identity SET Current_ID = @next WHERE Table_Name = @table",
            SqlSession.P("@next", id + 1), SqlSession.P("@table", "ST_Person"));
        return id;
    }

    private static string Format(string prefix, int value, int width)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Length < width) text = text.PadLeft(width, '0');
        return (prefix ?? string.Empty) + text;
    }

    private static (string prefix, int width) Defaults(SqlSession db)
    {
        var prefix = CardLookup.ConfigValue(db, "PersonNo.DefaultPrefix", "P");
        var width = CardLookup.ConfigInt(db, "PersonNo.DefaultWidth", 6);
        if (width < 1 || width > 12) width = 6;
        return (prefix, width);
    }
}

/// <summary>部门编号：优先调用 CS 的 st_pd_GetMaxDeptNo，缺失时按同一规则回退。</summary>
public static class CardDeptNo
{
    public static string Next(SqlSession db, string parentNo, out string error)
    {
        error = null;
        var parent = (parentNo ?? string.Empty).Trim();
        if (parent.Length > 0)
        {
            try
            {
                var value = db.Scalar("EXEC dbo.st_pd_GetMaxDeptNo @DeptNo = @parent", SqlSession.P("@parent", parent));
                var text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (text.Trim().Length > 0) return text.Trim();
            }
            catch
            {
                // 存储过程缺失时回退
            }
        }

        var max = db.Scalar(
            "SELECT MAX(Dept_No) FROM dbo.ST_Department WHERE Dept_No LIKE @like AND LEN(Dept_No) = LEN(@parent) + 2",
            SqlSession.P("@like", parent + "%"), SqlSession.P("@parent", parent));

        var index = 1;
        if (max != null)
        {
            var text = Convert.ToString(max, CultureInfo.InvariantCulture) ?? string.Empty;
            if (text.Length >= parent.Length + 2)
            {
                int parsed;
                if (int.TryParse(text.Substring(parent.Length, 2), out parsed)) index = parsed + 1;
            }
        }

        if (index > 99)
        {
            error = "部门编号已超过 99，无法自动生成";
            return null;
        }
        return parent + index.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>根部门前缀：配置优先，缺省取现有最顶层（4 位）编号前两位。</summary>
    public static string RootPrefix(SqlSession db)
    {
        var configured = CardLookup.ConfigValue(db, "Dept.RootPrefix", string.Empty);
        if (configured.Length >= 2) return configured.Substring(0, 2).ToUpperInvariant();

        var min = db.Scalar("SELECT MIN(Dept_No) FROM dbo.ST_Department WHERE LEN(Dept_No) = 4");
        var text = min == null ? string.Empty : Convert.ToString(min, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Length >= 2 ? text.Substring(0, 2) : "10";
    }

    /// <summary>
    /// 新增根部门：根编号 = 前缀（两位）+ 两位序号，共 4 位（如 1001、1009）。
    /// 与现场存量编号一致，逐级再追加两位。
    /// </summary>
    public static string NextRoot(SqlSession db, out string error)
    {
        error = null;
        var prefix = RootPrefix(db);

        var max = db.Scalar(
            "SELECT MAX(Dept_No) FROM dbo.ST_Department WHERE LEN(Dept_No) = 4 AND Dept_No LIKE @like",
            SqlSession.P("@like", prefix + "%"));

        var index = 1;
        if (max != null)
        {
            var text = Convert.ToString(max, CultureInfo.InvariantCulture) ?? string.Empty;
            if (text.Length == 4)
            {
                int parsed;
                if (int.TryParse(text.Substring(2, 2), out parsed)) index = parsed + 1;
            }
        }

        while (index <= 99)
        {
            var candidate = prefix + index.ToString("00", CultureInfo.InvariantCulture);
            if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Department WHERE Dept_No = @no", SqlSession.P("@no", candidate)) == 0)
                return candidate;
            index++;
        }

        error = "根部门编号已用尽，请手工指定部门编号";
        return null;
    }

    /// <summary>部门主键发号：优先调用 CS 存储过程，缺失时回退到 ST_Identity。</summary>
    public static int NextDeptId(SqlSession db)
    {
        try
        {
            var value = db.Scalar(
                "DECLARE @newId INT; " +
                "EXEC dbo.st_pd_GetMaxIDEx @TableName = 'ST_Department', @MaxTableID = @newId OUTPUT, @Increment = 1; " +
                "SELECT @newId;");
            if (value != null) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // 存储过程缺失时回退
        }

        var current = db.Scalar("SELECT Current_ID FROM dbo.ST_Identity WITH (UPDLOCK, ROWLOCK) WHERE Table_Name = 'ST_Department'");
        if (current == null)
        {
            var next = db.ScalarInt("SELECT ISNULL(MAX(Dept_ID), 0) FROM dbo.ST_Department") + 1;
            db.Exec("INSERT INTO dbo.ST_Identity (Table_Name, Current_ID) VALUES ('ST_Department', @next)", SqlSession.P("@next", next + 1));
            return next;
        }

        var id = Convert.ToInt32(current, CultureInfo.InvariantCulture);
        db.Exec("UPDATE dbo.ST_Identity SET Current_ID = @next WHERE Table_Name = 'ST_Department'", SqlSession.P("@next", id + 1));
        return id;
    }
}

/// <summary>人员与组织的查询行（字段名与前端一致）。</summary>
public class CardPersonRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public int? deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public string? cardNo { get; set; }
    public string? personType { get; set; }
    public string? personTypeName { get; set; }
    public string? certName { get; set; }
    public string? certCode { get; set; }
    public string? duty { get; set; }
    public string? dutyName { get; set; }
    public string? title { get; set; }
    public string? nationality { get; set; }
    public string? nationalityName { get; set; }
    public string? sex { get; set; }
    public DateTime? birthday { get; set; }
    public bool? isMarry { get; set; }
    public string? nativePlace { get; set; }
    public string? address { get; set; }
    public string? residence { get; set; }
    public string? onWorkDate { get; set; }
    public string? offWorkDate { get; set; }
    public bool isDel { get; set; }
    public string? remark { get; set; }
    public string? mobile { get; set; }
    public string? idNumber { get; set; }
    public DateTime? updateTime { get; set; }
    public int hasPhoto { get; set; }
    public string? cardTypeNo { get; set; }
    public string? cardTypeName { get; set; }
    public byte? cardState { get; set; }
    public decimal? cardBalance { get; set; }
    public byte faceStatus { get; set; }
}

public class CardNoProbe
{
    public string? personNo { get; set; }
    public string? cardNo { get; set; }
}

/// <summary>人员类型取值：ST_Person.Person_Type 为自由文本，这里按实际用到的值去重统计。</summary>
public class CardPersonTypeRow
{
    public string? personType { get; set; }
    public int personCount { get; set; }
}

public class CardDictItem
{
    public int id { get; set; }
    public string? tableName { get; set; }
    public string? dictId { get; set; }
    public string? dictValue { get; set; }
    public bool? sysCol { get; set; }
    public string? remark { get; set; }
    public int refCount { get; set; }
}

/// <summary>部门台账导出的行数据，字段与 ST_Department 及人员统计对齐。</summary>
public class CardDeptExportRow
{
    public int deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public int? parentId { get; set; }
    public string? parentNo { get; set; }
    public string? parentName { get; set; }
    public int level { get; set; }
    public string? chief { get; set; }
    public string? telphone { get; set; }
    public string? duty { get; set; }
    public string? deptType { get; set; }
    public int activeCount { get; set; }
    public int activeDirect { get; set; }
    public int deletedCount { get; set; }
    public int childCount { get; set; }
    public bool? isDel { get; set; }
    public int? sortNo { get; set; }
    public string? remark { get; set; }
}

/// <summary>
/// 人脸照片自动审核：压缩与判定口径与 MobileFaceController 的 CompressUnder80K/FaceReview 完全一致，
/// 供人脸照片库上传与批量导入复用，避免移动端与卡务中心出现两套判定标准。
/// </summary>
public static class FaceAutoReview
{
    /// <summary>人脸照片上限：80K（81920 字节）。</summary>
    public const int MaxPhotoBytes = 80 * 1024;

    /// <summary>自动审核写入审核人字段的标识。</summary>
    public const string AutoUser = "SYSTEM";

    /// <summary>是否 JPEG（FF D8 FF），未超限的 JPEG 可直接沿用、避免无谓二次压缩。</summary>
    public static bool IsJpeg(byte[] raw)
        => raw != null && raw.Length > 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF;

    /// <summary>压缩到 80K 以下：先按长边逐级缩小，再逐级降低 JPEG 质量，直至体积达标。</summary>
    public static byte[] CompressUnder80K(byte[] raw)
    {
        if (raw == null || raw.Length == 0) return raw!;
        if (raw.Length <= MaxPhotoBytes && IsJpeg(raw)) return raw;
        try
        {
            using var ms = new MemoryStream(raw);
            using var bmp = new Bitmap(ms);
            ApplyExifOrientation(bmp);   // 关键：先把方向应用到像素

            int[] longSides = { 1080, 720, 480, 320, 240 };
            foreach (int side in longSides)
            {
                byte[] c = EncodeJpegScaled(bmp, side, 80);
                if (c != null && c.Length < MaxPhotoBytes) return c;
            }
            int[] qualities = { 90, 80, 70, 60, 50, 40, 30, 20 };
            foreach (int q in qualities)
            {
                byte[] c = EncodeJpegScaled(bmp, 480, q);
                if (c != null && c.Length < MaxPhotoBytes) return c;
            }
            return EncodeJpegScaled(bmp, 240, 15) ?? raw;
        }
        catch { return raw; }
    }

    /// <summary>
    /// 自动审核：过小/无法解析/分辨率低/过暗过曝/内容空白/未检测到人脸/人脸朝向不合格 直接驳回。
    /// 人脸与姿态判定使用 PicoDetector，与 H5/小程序同一套参数与阈值。
    /// </summary>
    public static bool Review(byte[] img, out string reason)
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

    /// <summary>按长边上限等比缩放并编码 JPEG（质量为 1-100）。</summary>
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
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
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
}
