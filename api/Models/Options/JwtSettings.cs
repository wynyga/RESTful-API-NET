using System.ComponentModel.DataAnnotations;

namespace Models.Options
{
    /// <summary>
    /// Token signing settings, bound from the "JwtSettings" configuration section
    /// (environment variables such as JwtSettings__Secret). Validated when the app starts,
    /// so a missing or weak secret stops the process instead of failing on the first login.
    /// </summary>
    public sealed class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        [Required]
        [MinLength(32, ErrorMessage = "JwtSettings:Secret must be at least 32 characters (HMAC-SHA256 key).")]
        public string Secret { get; set; } = string.Empty;

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int ExpirationInMinutes { get; set; } = 60;
    }
}
