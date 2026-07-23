using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using EtlAnalytics.RulesEngine.Interfaces;

namespace EtlAnalytics.RulesEngine.Services;

/// <summary>
/// Provides AES-256 encryption and decryption services using PBKDF2 for key derivation.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesEncryptionService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration to resolve the encryption key.</param>
    public AesEncryptionService(IConfiguration configuration)
    {
        var keyString = Environment.GetEnvironmentVariable("DB_ENCRYPTION_KEY") 
            ?? configuration["Security:EncryptionKey"];

        // Use PBKDF2 for more secure key derivation from a password/string
        var salt = Encoding.UTF8.GetBytes("EtlAnalytics.Salt.RulesEngine"); // Fixed salt for library consistency
        _key = Rfc2898DeriveBytes.Pbkdf2(keyString ?? string.Empty, salt, 100000, HashAlgorithmName.SHA256, 32);
    }

    /// <summary>
    /// Encrypts the specified plain text using AES-256.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The Base64-encoded encrypted string, including the IV.</returns>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, iv);
        using var ms = new MemoryStream();
        
        // Write IV at the beginning
        ms.Write(iv, 0, iv.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts the specified cipher text using AES-256.
    /// </summary>
    /// <param name="cipherText">The Base64-encoded cipher text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    /// <exception cref="CryptographicException">Thrown when decryption fails.</exception>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            throw new CryptographicException("Failed to decrypt cipher text.", ex);
        }
    }
}
