// pico 人脸检测器（C# 移植版）。
// 与 MicroH5/src/lib/facedetect/pico.js、MicroH5-miniprogram/utils/facedetect/pico.js
// 使用同一套级联模型（PicoCascade.FacefinderBase64）与同一套参数，保证 H5 / 小程序 / 后台
// 三端「照片里有没有人脸」的判定口径一致。
// 来源：pico / pico.js 项目（https://github.com/tehnokv/picojs，MIT License）。
// 注意：本类只回答「照片里有没有人脸」，不做 1:N 身份比对。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace PsnMgrService.FaceDetect
{
    /// <summary>pico 级联人脸检测器（纯托管实现，不依赖外部模型服务）。</summary>
    internal static class PicoDetector
    {
        /// <summary>检测前把图片等比缩放，使长边不超过该值</summary>
        internal const int MaxSide = 480;

        /// <summary>最小人脸边长（检测图坐标系），小于该边长的检出结果视为噪声</summary>
        internal const int MinSize = 30;

        /// <summary>级联打分阈值：高于该分数才认为检出了人脸</summary>
        internal const double ScoreThreshold = 5.0;

        /// <summary>检测窗口每次移动的比例</summary>
        internal const double ShiftFactor = 0.1;

        /// <summary>多尺度检测的缩放比例</summary>
        internal const double ScaleFactor = 1.1;

        /// <summary>聚类时的 IoU 阈值（非极大值抑制）</summary>
        internal const double IouThreshold = 0.2;

        private static readonly Lazy<Cascade> Model =
            new Lazy<Cascade>(() => Unpack(Convert.FromBase64String(PicoCascade.FacefinderBase64)), true);

        /// <summary>解析后的级联模型</summary>
        private sealed class Cascade
        {
            internal int Tdepth;
            internal int Ntrees;
            internal int Pow2Tdepth;
            internal sbyte[] Tcodes;
            internal float[] Tpreds;
            internal float[] Thresh;
        }

        /// <summary>单次检测结果</summary>
        internal sealed class Result
        {
            /// <summary>是否检出了人脸</summary>
            internal bool Detected;
            /// <summary>检出的人脸数量</summary>
            internal int FaceCount;
            /// <summary>最高得分（未检出为 0）</summary>
            internal double TopScore;
            /// <summary>最大人脸边长（检测图坐标系，未检出为 0）</summary>
            internal double TopSize;
        }

        /// <summary>判断位图中是否包含人脸（内部会等比缩放到长边 480 后检测）。</summary>
        internal static bool HasFace(Bitmap bmp)
        {
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0) return false;
            int width, height;
            byte[] rgba = ToRgba(bmp, out width, out height);
            if (rgba == null) return false;
            return Detect(rgba, width, height).Detected;
        }

        /// <summary>对 RGBA 像素执行检测（供内部复用与离线比对）。</summary>
        internal static Result Detect(byte[] rgba, int width, int height)
        {
            var result = new Result();
            if (rgba == null || width <= 0 || height <= 0) return result;
            if (rgba.Length < width * height * 4) return result;

            int sw, sh;
            byte[] scaled = Downscale(rgba, width, height, MaxSide, out sw, out sh);
            byte[] gray = GrayFromRgba(scaled, sw, sh);

            var dets = RunCascade(gray, sw, sh, Model.Value);
            var clusters = Cluster(dets, IouThreshold);

            foreach (double[] d in clusters)
            {
                if (d[3] > ScoreThreshold && d[2] >= MinSize)
                {
                    result.FaceCount++;
                    if (d[3] > result.TopScore)
                    {
                        result.TopScore = d[3];
                        result.TopSize = d[2];
                    }
                }
            }
            result.Detected = result.FaceCount > 0;
            if (!result.Detected) { result.TopScore = 0; result.TopSize = 0; }
            return result;
        }

        /// <summary>Bitmap → RGBA 字节数组（保持原始尺寸）。</summary>
        private static byte[] ToRgba(Bitmap bmp, out int width, out int height)
        {
            width = bmp.Width;
            height = bmp.Height;
            try
            {
                using (Bitmap src = bmp.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppArgb))
                {
                    var rect = new Rectangle(0, 0, width, height);
                    BitmapData data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        byte[] rgba = new byte[width * height * 4];
                        int stride = data.Stride;
                        byte[] row = new byte[stride];
                        for (int y = 0; y < height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                IntPtr.Add(data.Scan0, y * stride), row, 0, stride);
                            for (int x = 0; x < width; x++)
                            {
                                int si = x * 4;
                                int o = (y * width + x) * 4;
                                // 32bppArgb 在内存中为 BGRA
                                rgba[o] = row[si + 2];
                                rgba[o + 1] = row[si + 1];
                                rgba[o + 2] = row[si];
                                rgba[o + 3] = 255;
                            }
                        }
                        return rgba;
                    }
                    finally { src.UnlockBits(data); }
                }
            }
            catch { return null; }
        }

        /// <summary>与 JS Math.round 一致的四舍五入（.5 向上，避免与 ToEven 产生 1px 差异）。</summary>
        private static int JsRound(double v)
        {
            return (int)Math.Floor(v + 0.5);
        }

        /// <summary>面积平均降采样（长边未超过 maxSide 时原样返回）。</summary>
        private static byte[] Downscale(byte[] rgba, int width, int height, int maxSide, out int outW, out int outH)
        {
            double scale = Math.Min(1.0, (double)maxSide / Math.Max(width, height));
            int tw = Math.Max(1, JsRound(width * scale));
            int th = Math.Max(1, JsRound(height * scale));
            outW = tw;
            outH = th;
            if (tw == width && th == height) return rgba;

            byte[] outp = new byte[tw * th * 4];
            double bw = (double)width / tw;
            double bh = (double)height / th;
            for (int y = 0; y < th; y++)
            {
                int y0 = (int)Math.Floor(y * bh);
                int y1 = (int)Math.Min(height, Math.Ceiling((y + 1) * bh));
                for (int x = 0; x < tw; x++)
                {
                    int x0 = (int)Math.Floor(x * bw);
                    int x1 = (int)Math.Min(width, Math.Ceiling((x + 1) * bw));
                    long r = 0, g = 0, b = 0;
                    int n = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        for (int xx = x0; xx < x1; xx++)
                        {
                            int i = (yy * width + xx) * 4;
                            r += rgba[i];
                            g += rgba[i + 1];
                            b += rgba[i + 2];
                            n++;
                        }
                    }
                    int o = (y * tw + x) * 4;
                    // 与 Uint8ClampedArray 一致：四舍五入（.5 取偶）
                    outp[o] = (byte)Math.Round((double)r / n, MidpointRounding.ToEven);
                    outp[o + 1] = (byte)Math.Round((double)g / n, MidpointRounding.ToEven);
                    outp[o + 2] = (byte)Math.Round((double)b / n, MidpointRounding.ToEven);
                    outp[o + 3] = 255;
                }
            }
            return outp;
        }

        /// <summary>RGBA → 灰度（与 pico 一致：gray = (2R + 7G + B) / 10，向下取整）。</summary>
        private static byte[] GrayFromRgba(byte[] rgba, int width, int height)
        {
            byte[] gray = new byte[width * height];
            for (int i = 0; i < width * height; i++)
                gray[i] = (byte)((2 * rgba[i * 4] + 7 * rgba[i * 4 + 1] + rgba[i * 4 + 2]) / 10);
            return gray;
        }

        /// <summary>解析级联二进制（与 pico.js unpack_cascade 的字节布局与索引语义完全一致）。</summary>
        private static Cascade Unpack(byte[] bytes)
        {
            var c = new Cascade();
            int p = 8; // 跳过 8 字节头（版本号 + 训练期数据）
            c.Tdepth = BitConverter.ToInt32(bytes, p); p += 4;
            c.Ntrees = BitConverter.ToInt32(bytes, p); p += 4;
            c.Pow2Tdepth = 1 << c.Tdepth;

            c.Tcodes = new sbyte[c.Ntrees * 4 * c.Pow2Tdepth];
            c.Tpreds = new float[c.Ntrees * c.Pow2Tdepth];
            c.Thresh = new float[c.Ntrees];

            for (int t = 0; t < c.Ntrees; t++)
            {
                int baseIdx = t * 4 * c.Pow2Tdepth;
                for (int k = 0; k < 4 * c.Pow2Tdepth - 4; k++)
                    c.Tcodes[baseIdx + 4 + k] = (sbyte)(bytes[p + k] > 127 ? bytes[p + k] - 256 : bytes[p + k]);
                p += 4 * c.Pow2Tdepth - 4;
                for (int k = 0; k < c.Pow2Tdepth; k++)
                {
                    c.Tpreds[t * c.Pow2Tdepth + k] = BitConverter.ToSingle(bytes, p);
                    p += 4;
                }
                c.Thresh[t] = BitConverter.ToSingle(bytes, p);
                p += 4;
            }
            return c;
        }

        /// <summary>单窗口分类打分；返回 -1 表示该窗口未通过级联（与 pico.js classify_region 等价）。</summary>
        private static double Classify(Cascade c, int r0, int c0, double s, byte[] pixels, int ldim)
        {
            // 注意：s 保持 double（scale 是 30 × 1.1^n 的浮点数），与 JS 端一致，
            // 不能在入口处把 scale 截断成整数，否则 tcodes*s 的取值会偏差。
            double r = 256.0 * r0;
            double cCol = 256.0 * c0;
            int root = 0;
            double o = 0;
            int len = pixels.Length;
            for (int i = 0; i < c.Ntrees; i++)
            {
                int idx = 1;
                for (int j = 0; j < c.Tdepth; j++)
                {
                    // 注意：与 pico.js 一致，tcodes 使用本次更新前的 idx
                    int off = root + 4 * idx;
                    int ia = JsShift8(r + c.Tcodes[off] * s) * ldim + JsShift8(cCol + c.Tcodes[off + 1] * s);
                    int ib = JsShift8(r + c.Tcodes[off + 2] * s) * ldim + JsShift8(cCol + c.Tcodes[off + 3] * s);
                    // 越界时 JS 取到 undefined，比较结果为 false，此处保持同样语义
                    bool less = ia >= 0 && ia < len && ib >= 0 && ib < len && pixels[ia] <= pixels[ib];
                    idx = 2 * idx + (less ? 1 : 0);
                }
                o += c.Tpreds[c.Pow2Tdepth * i + idx - c.Pow2Tdepth];
                if (o <= c.Thresh[i]) return -1;
                root += 4 * c.Pow2Tdepth;
            }
            return o - c.Thresh[c.Ntrees - 1];
        }

        /// <summary>等价的 JS 算术右移 8 位（含 ToInt32 截断语义，scale 的小数部分要参与运算）。</summary>
        private static int JsShift8(double v)
        {
            return ((int)v) >> 8;
        }

        /// <summary>多尺度滑窗扫描（使用人脸检测的默认参数），返回 [r, c, s, q] 四元组。</summary>
        private static List<double[]> RunCascade(byte[] pixels, int ncols, int nrows, Cascade c)
        {
            return RunCascade(pixels, ncols, nrows, c, MinSize, ShiftFactor, ScaleFactor);
        }

        /// <summary>
        /// 多尺度滑窗扫描（参数化，供姿态分析使用不同尺度网格），返回 [r, c, s, q] 四元组。
        /// 与前端 runCascade(image, classifyRegion, params) 的语义一致。
        /// </summary>
        private static List<double[]> RunCascade(byte[] pixels, int ncols, int nrows, Cascade c, double minSize, double shiftFactor, double scaleFactor)
        {
            var detections = new List<double[]>();
            double scale = minSize;
            double maxSize = JsRound(Math.Min(nrows, ncols) * 0.95);
            while (scale <= maxSize)
            {
                int step = Math.Max((int)(shiftFactor * scale), 1);
                int offset = ((int)(scale / 2)) + 1;
                for (int r = offset; r <= nrows - offset; r += step)
                {
                    for (int col = offset; col <= ncols - offset; col += step)
                    {
                        double q = Classify(c, r, col, scale, pixels, ncols);
                        if (q > 0.0) detections.Add(new double[] { r, col, scale, q });
                    }
                }
                scale *= scaleFactor;
            }
            return detections;
        }

        /// <summary>非极大值抑制：把重叠的检出结果合并为一个簇。</summary>
        private static List<double[]> Cluster(List<double[]> dets, double iouThreshold)
        {
            // 稳定排序（同分时保持检出顺序），与 JS Array.sort 的行为一致
            var sorted = dets.Select((d, i) => new { Item = d, Index = i })
                .OrderByDescending(x => x.Item[3])
                .ThenBy(x => x.Index)
                .Select(x => x.Item)
                .ToList();

            bool[] assigned = new bool[sorted.Count];
            var clusters = new List<double[]>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (assigned[i]) continue;
                double r = 0, c = 0, s = 0, q = 0;
                int n = 0;
                for (int j = i; j < sorted.Count; j++)
                {
                    if (Iou(sorted[i], sorted[j]) > iouThreshold)
                    {
                        assigned[j] = true;
                        r += sorted[j][0];
                        c += sorted[j][1];
                        s += sorted[j][2];
                        q += sorted[j][3];
                        n++;
                    }
                }
                clusters.Add(new double[] { r / n, c / n, s / n, q });
            }
            return clusters;
        }

        private static double Iou(double[] d1, double[] d2)
        {
            double overR = Math.Max(0, Math.Min(d1[0] + d1[2] / 2, d2[0] + d2[2] / 2) - Math.Max(d1[0] - d1[2] / 2, d2[0] - d2[2] / 2));
            double overC = Math.Max(0, Math.Min(d1[1] + d1[2] / 2, d2[1] + d2[2] / 2) - Math.Max(d1[1] - d1[2] / 2, d2[1] - d2[2] / 2));
            return (overR * overC) / (d1[2] * d1[2] + d2[2] * d2[2] - overR * overC);
        }

        // ===== 人脸姿态判定（与前端 pico.js 的 FACE_POSE / detectFacePose 完全一致） =====
        // 原理：把灰度图按候选朝向旋转后重跑同一级联分类器，得分最高的朝向即人脸朝向。

        /// <summary>姿态合格阈值（度）：人脸偏转不超过该角度视为正立，同时用于拼装驳回文案</summary>
        internal const int PoseUprightMaxDeg = 20;

        /// <summary>候选朝向（度）：0 为合格朝向，±40 检验是否偏转超过 20°，±90 为横向，180 为倒置</summary>
        internal static readonly int[] PoseAngles = new int[] { 0, -40, 40, -90, 90, 180 };

        /// <summary>
        /// 相对余量：其它朝向得分需比 0° 高出该比例才判定为异常朝向。
        /// 实测标定（真实偏转 → ±40 朝向与 0° 的得分比，5 张样张）：18° ≈ 0、20° ≈ 1.00–1.31、
        /// 21° ≈ 1.10–1.39、22° ≈ 1.40–1.54、25° ≈ 1.99–3.92、30° ≈ 7–19；
        /// 取 1.35 倍正好落在 20° 与 22° 的实测区间之间，使 ≤20° 稳定通过、≥22° 稳定驳回。
        /// </summary>
        internal const double PoseScoreMargin = 0.35;

        /// <summary>姿态分析的分辨率（低于检测分辨率，用于控制耗时）</summary>
        internal const int PoseMaxSide = 320;

        /// <summary>姿态分析的最小人脸边长（随 maxSide 等比缩小）</summary>
        internal const int PoseMinSize = 20;

        /// <summary>姿态不合格的驳回文案（与前端 POSE_REASONS 同文）</summary>
        internal const string PoseReasonSideways = "检测到照片中的人脸呈横向，请将照片竖直正放后正对镜头重新拍摄";

        /// <summary>姿态不合格的驳回文案（与前端 POSE_REASONS 同文）</summary>
        internal const string PoseReasonUpsideDown = "检测到照片中的人脸倒置，请正对镜头重新拍摄";

        /// <summary>姿态不合格的驳回文案（与前端 POSE_REASONS 同文）</summary>
        internal static readonly string PoseReasonTilted =
            "检测到人脸偏转超过" + PoseUprightMaxDeg + "°，请保持头部端正、正对镜头重新拍摄";

        /// <summary>人脸姿态判定结果</summary>
        internal sealed class PoseResult
        {
            /// <summary>任一朝向下是否检出人脸</summary>
            internal bool Detected;
            /// <summary>0° 是否检出且其它朝向未明显更优（朝向合格）</summary>
            internal bool Upright;
            /// <summary>与 Upright 同义，供调用方按「是否合格」阅读</summary>
            internal bool Ok;
            /// <summary>姿态：upright / tilted / sideways / upside-down / unknown</summary>
            internal string Pose = "unknown";
            /// <summary>得分最高的探测角度</summary>
            internal int Angle;
            /// <summary>0° 朝向的最高得分</summary>
            internal double Score0;
            /// <summary>所有朝向中的最高得分</summary>
            internal double BestScore;
            /// <summary>各朝向检出的人脸数合计</summary>
            internal int FaceCount;
        }

        /// <summary>
        /// 判断位图中人脸的朝向（横向 / 倒置 / 倾斜超限 / 正立）。
        /// 内部按与前端相同的方式先把位图缩放到长边 480，再交给 DetectPose 分析。
        /// </summary>
        internal static PoseResult Pose(Bitmap bmp)
        {
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0) return new PoseResult();
            int width, height;
            byte[] rgba = ToRgba(bmp, out width, out height);
            if (rgba == null) return new PoseResult();
            int sw, sh;
            byte[] scaled = Downscale(rgba, width, height, MaxSide, out sw, out sh);
            return DetectPose(scaled, sw, sh);
        }

        /// <summary>对（长边不超过 480 的）RGBA 像素执行姿态分析。</summary>
        internal static PoseResult DetectPose(byte[] rgba, int width, int height)
        {
            var result = new PoseResult();
            if (rgba == null || width <= 0 || height <= 0) return result;
            if (rgba.Length < width * height * 4) return result;

            int sw, sh;
            byte[] scaled = Downscale(rgba, width, height, PoseMaxSide, out sw, out sh);
            byte[] gray = GrayFromRgba(scaled, sw, sh);

            var scores = new Dictionary<int, double>();
            double score0 = 0;
            double bestScore = -1;
            int bestAngle = 0;
            int faceCount = 0;

            for (int i = 0; i < PoseAngles.Length; i++)
            {
                int angle = PoseAngles[i];
                int rw, rh;
                byte[] rotated = RotateGray(gray, sw, sh, angle, out rw, out rh);
                double[] top = TopScore(rotated, rw, rh);
                double score = top[0];
                scores[angle] = score;
                if (angle == 0) score0 = score;
                if (score > 0)
                {
                    result.Detected = true;
                    faceCount += (int)top[2];
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }

            // 0° 检出、且其它朝向的得分都没有明显更高 → 人脸正立且偏转不超过阈值
            bool upright = false;
            if (score0 > 0)
            {
                upright = true;
                for (int i = 0; i < PoseAngles.Length; i++)
                {
                    int angle = PoseAngles[i];
                    if (angle != 0 && scores[angle] > score0 * (1 + PoseScoreMargin))
                    {
                        upright = false;
                        break;
                    }
                }
            }

            string pose = "unknown";
            if (result.Detected)
            {
                if (upright) pose = "upright";
                else if (bestAngle == 90 || bestAngle == -90) pose = "sideways";
                else if (bestAngle == 180) pose = "upside-down";
                else pose = "tilted";
            }

            result.Upright = upright;
            result.Ok = upright;
            result.Pose = pose;
            result.Angle = bestAngle;
            result.Score0 = score0;
            result.BestScore = bestScore > 0 ? bestScore : 0;
            result.FaceCount = faceCount;
            return result;
        }

        /// <summary>供离线比对使用的降采样透出（不在业务流程中使用）。</summary>
        internal static byte[] DownscaleForTest(byte[] rgba, int width, int height, int maxSide, out int outW, out int outH)
        {
            return Downscale(rgba, width, height, maxSide, out outW, out outH);
        }

        /// <summary>供离线比对使用的灰度转换透出（不在业务流程中使用）。</summary>
        internal static byte[] GrayForTest(byte[] rgba, int width, int height)
        {
            return GrayFromRgba(rgba, width, height);
        }

        /// <summary>单张灰度图的人脸最高得分，返回 [score, size, count]（score 为 0 表示未检出）。</summary>
        internal static double[] TopScore(byte[] pixels, int ncols, int nrows)
        {
            var dets = RunCascade(pixels, ncols, nrows, Model.Value, PoseMinSize, ShiftFactor, ScaleFactor);
            var clusters = Cluster(dets, IouThreshold);
            var top = new double[] { 0, 0, 0 };
            for (int i = 0; i < clusters.Count; i++)
            {
                double[] d = clusters[i];
                if (d[3] > ScoreThreshold && d[2] >= PoseMinSize)
                {
                    top[2] += 1;
                    if (d[3] > top[0])
                    {
                        top[0] = d[3];
                        top[1] = d[2];
                    }
                }
            }
            return top;
        }

        /// <summary>定点旋转用的 Q16 余弦/正弦（与前端 POSE_ROTATION 同值）。</summary>
        private static void PoseRotation(int angle, out int cosQ, out int sinQ)
        {
            if (angle == 40) { cosQ = 50203; sinQ = 42126; return; }
            cosQ = 50203;
            sinQ = -42126;
        }

        /// <summary>
        /// 旋转灰度图（与前端 rotateGray 完全一致，纯整数运算）：
        /// ±90 与 180 用精确的行列映射（±90 同时交换宽高），其余角度用中心定点旋转 + 最近邻采样，越界填 0。
        /// </summary>
        internal static byte[] RotateGray(byte[] gray, int width, int height, int angle, out int outW, out int outH)
        {
            if (angle == 0)
            {
                outW = width;
                outH = height;
                return gray;
            }

            if (angle == 180)
            {
                outW = width;
                outH = height;
                byte[] flip = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    int src = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++) flip[y * width + x] = gray[src + (width - 1 - x)];
                }
                return flip;
            }

            if (angle == 90 || angle == -90)
            {
                int w = height;
                int h = width;
                outW = w;
                outH = h;
                byte[] trans = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        trans[y * w + x] = angle == 90
                            ? gray[x * width + y]
                            : gray[(height - 1 - x) * width + (width - 1 - y)];
                    }
                }
                return trans;
            }

            int cosQ, sinQ;
            PoseRotation(angle, out cosQ, out sinQ);
            outW = width;
            outH = height;
            byte[] outp = new byte[width * height];
            int cx = width / 2;
            int cy = height / 2;
            for (int y = 0; y < height; y++)
            {
                int dy = y - cy;
                for (int x = 0; x < width; x++)
                {
                    int dx = x - cx;
                    int sx = cx + ((dx * cosQ + dy * sinQ) >> 16);
                    int sy = cy + ((-dx * sinQ + dy * cosQ) >> 16);
                    outp[y * width + x] = (sx >= 0 && sx < width && sy >= 0 && sy < height)
                        ? gray[sy * width + sx]
                        : (byte)0;
                }
            }
            return outp;
        }
    }
}