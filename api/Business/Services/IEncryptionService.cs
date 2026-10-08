namespace Business.Services
{
    public interface IEncryptionService
    {
        string EncryptId(int id);

        /// <exception cref="ArgumentException">The value is not a valid obfuscated id.</exception>
        int DecryptId(string encryptedId);

        bool TryDecryptId(string encryptedId, out int id);
    }
}
