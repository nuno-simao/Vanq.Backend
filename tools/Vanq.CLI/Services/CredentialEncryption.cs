using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vanq.CLI.Services;

/// <summary>
/// Cross-platform credential encryption service.
/// Windows: Uses DPAPI
/// Linux/macOS: Uses AES-256 with machine-specific key
/// </summary>
public static class CredentialEncryption
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("Vanq.CLI.Credentials.v1");

    public static byte[] Encrypt<T>(T data) where T : class
    {
        var json = JsonSerializer.Serialize(data);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        if (OperatingSystem.IsWindows())
        {
            return EncryptWithDPAPI(plainBytes);
        }
        else
        {
            return EncryptWithAES(plainBytes);
        }
    }

    public static T? Decrypt<T>(byte[] encryptedData) where T : class
    {
        try
        {
            byte[] plainBytes;

            if (OperatingSystem.IsWindows())
            {
                plainBytes = DecryptWithDPAPI(encryptedData);
            }
            else
            {
                plainBytes = DecryptWithAES(encryptedData);
            }

            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] EncryptWithDPAPI(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return System.Security.Cryptography.ProtectedData.Protect(
                data,
                AdditionalEntropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );
        }

        throw new PlatformNotSupportedException("DPAPI is only supported on Windows");
    }

    private static byte[] DecryptWithDPAPI(byte[] encryptedData)
    {
        if (OperatingSystem.IsWindows())
        {
            return System.Security.Cryptography.ProtectedData.Unprotect(
                encryptedData,
                AdditionalEntropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );
        }

        throw new PlatformNotSupportedException("DPAPI is only supported on Windows");
    }

    private static byte[] EncryptWithAES(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return result;
    }

    private static byte[] DecryptWithAES(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();

        // Extract IV from prepended bytes
        var iv = new byte[aes.IV.Length];
        var cipherText = new byte[encryptedData.Length - iv.Length];

        Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(encryptedData, iv.Length, cipherText, 0, cipherText.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    private static byte[] DeriveKey()
    {
        // Use machine-specific data to derive encryption key
        var machineKey = Environment.MachineName + Environment.UserName;
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineKey + "Vanq.CLI.Key.v1"));
    }
}
