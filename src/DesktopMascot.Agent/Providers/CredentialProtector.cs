using System.Security.Cryptography;
using System.Text;

namespace DesktopMascot.Agent.Providers;

/// <summary>
/// 使用 Windows DPAPI 加密/解密敏感凭证，避免明文存储在内存中
/// </summary>
public static class CredentialProtector
{
    /// <summary>
    /// 加密明文 API Key（DPAPI — 仅限当前用户/当前机器）
    /// </summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 解密为明文（仅在需要使用时调用，用完立即丢弃）
    /// </summary>
    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // 如果不是 Base64 编码，可能是旧版明文 key，直接返回
            return cipherText;
        }
        catch (CryptographicException)
        {
            // DPAPI 解密失败（可能换了用户/机器），返回原始值
            return cipherText;
        }
    }
}
