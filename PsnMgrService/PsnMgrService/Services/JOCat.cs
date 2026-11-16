namespace PsnMgrService.Services;

/// <summary>
/// 一卡通二维码工具（对应 Net 版 PayOrderService.cs 里的 JOCat）。
/// 扫码枪/闸机认的格式：16 进制卡号(8) + 开始时间(14) + 结束时间(14) + CRC16(4)。
/// 仅在 Web.config/appsettings 的 YKT_PaymentCode=1 时用于生成付款码/通行码，
/// 与 `employee;工号;时间戳` 那套并存，按现场闸机型号二选一。
/// </summary>
public static class JOCat
{
    /// <summary>十六进制卡号转整数</summary>
    public static uint TranCardNo(string hex, bool isHex = false)
        => isHex ? Convert.ToUInt32(hex, 16) : Convert.ToUInt32(hex);

    /// <summary>卡号转 8 位十六进制</summary>
    public static string TranCardNoToHex(uint cardNo) => cardNo.ToString("X8");

    /// <summary>CRC16（Modbus 多项式 0xA001），返回 4 位大写十六进制</summary>
    public static string Crc16(string strInput)
    {
        ushort crc = 0xffff;
        foreach (byte b in HexToBytes(strInput))
        {
            crc ^= b;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                else crc >>= 1;
            }
        }
        return crc.ToString("X4");
    }

    private static byte[] HexToBytes(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length % 2 != 0) return Array.Empty<byte>();
        var bytes = new byte[s.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        return bytes;
    }

    /// <summary>生成一卡通二维码字符串；失败返回空串</summary>
    public static string QrCode(uint cardNo, DateTime bgnTime, DateTime endTime)
    {
        try
        {
            string hexCard = TranCardNoToHex(cardNo);
            string src = hexCard + bgnTime.ToString("yyyyMMddHHmmss") + endTime.ToString("yyyyMMddHHmmss");
            return src + Crc16(src).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }
}
