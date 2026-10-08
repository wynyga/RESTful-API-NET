namespace Models.DTOs
{
    public class UserListDTO
    {
        public string Id { get; set; } = string.Empty; // Encrypted ID
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
