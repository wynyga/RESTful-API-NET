using HashidsNet;
using Microsoft.Extensions.Configuration;

namespace Business.Services
{
    /// <summary>
    /// Turns database ids into short opaque strings (Hashids). This hides sequential ids from
    /// clients; it is obfuscation, not encryption, so it must never replace authorisation checks.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private const int MinSaltLength = 8;
        private readonly Hashids _hashids;

        public EncryptionService(IConfiguration configuration)
        {
            var salt = configuration["HASHIDS_SALT"];
            if (string.IsNullOrWhiteSpace(salt) || salt.Length < MinSaltLength)
            {
                // No built-in fallback: a published default salt would make the ids guessable.
                throw new InvalidOperationException($"HASHIDS_SALT must be set to at least {MinSaltLength} characters.");
            }
            _hashids = new Hashids(salt, minHashLength: 8);
        }

        public string EncryptId(int id) => _hashids.Encode(id);

        public int DecryptId(string encryptedId)
        {
            return TryDecryptId(encryptedId, out var id)
                ? id
                : throw new ArgumentException("Invalid encrypted ID");
        }

        public bool TryDecryptId(string encryptedId, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(encryptedId)) return false;

            int[] decoded;
            try
            {
                decoded = _hashids.Decode(encryptedId);
            }
            catch (Exception)
            {
                return false; // malformed input is "not found", never a 500
            }
            if (decoded.Length != 1) return false;

            id = decoded[0];
            return true;
        }
    }
}
