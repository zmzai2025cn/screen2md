using System.Security.Cryptography;
using System.Text;

namespace Screen2MD.Core.Security;

/// <summary>
/// 安全工具类 - 加密、哈希、敏感数据处理
/// </summary>
public static class SecurityUtils
{
    /// <summary>
    /// 生成安全的随机字符串
    /// </summary>    public static string GenerateSecureToken(int length = 32)
    {
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 敏感信息脱敏
    /// </summary>    public static string MaskSensitiveData(string? input, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (input.Length <= visibleChars * 2) return "***";

        return $"{input[..visibleChars]}***{input[^visibleChars..]}";
    }

    /// <summary>
    /// 检测并过滤敏感信息
    /// </summary>    public static string SanitizeSensitiveInfo(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = input;

        // 银行卡号（16-19位数字）
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6(?:011|5[0-9]{2})[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\d{3})\d{11})\b",
            "[CARD_MASKED]");

        // 身份证号（18位）
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b[1-9]\d{5}(?:18|19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[\dXx]\b",
            "[ID_MASKED]");

        // 邮箱地址
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "[EMAIL_MASKED]");

        // 密码模式（Password: xxx, Pwd: xxx）
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?i)(password|pwd|pass|secret)\s*[:=]\s*\S+",
            "[PASSWORD_MASKED]");

        return result;
    }

    /// <summary>
    /// 验证路径安全（防止目录遍历）
    /// </summary>    public static bool IsPathSafe(string path, string basePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullBasePath = Path.GetFullPath(basePath);

            // 确保路径在基目录下
            return fullPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全的文件名生成
    /// </summary>    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "unnamed";

        // 移除路径分隔符和危险字符
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalid.Contains(c) && c > 31)
            .ToArray());

        // 防止空文件名
        if (string.IsNullOrWhiteSpace(sanitized))
            return "unnamed";

        // 限制长度
        if (sanitized.Length > 200)
            sanitized = sanitized[..200];

        return sanitized;
    }

    /// <summary>
    /// 计算文件哈希（用于完整性验证）
    /// </summary>    public static async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }
}
