namespace Models.DTOs
{
    public class UserProfileDTO
    {
        /// <summary>Obfuscated user id (never the raw database key).</summary>
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
