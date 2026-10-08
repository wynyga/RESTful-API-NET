using System.ComponentModel.DataAnnotations;

namespace Models.DTOs
{
    public class RegisterRequestDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6, ErrorMessage = "Password minimal 6 karakter.")]
        public string Password { get; set; } = string.Empty;
    }
}
