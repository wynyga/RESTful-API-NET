namespace Models.DTOs
{
    public class LoginResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}

