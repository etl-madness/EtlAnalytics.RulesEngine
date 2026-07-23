namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Defines the contract for an encryption service to protect sensitive data.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypts the specified plain text.</summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <returns>The encrypted cipher text.</returns>
    string Encrypt(string plainText);
    /// <summary>Decrypts the specified cipher text.</summary>
    /// <param name="cipherText">The text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    string Decrypt(string cipherText);
}
