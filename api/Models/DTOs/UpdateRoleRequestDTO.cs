using System.ComponentModel.DataAnnotations;

namespace Models.DTOs
{
    public class UpdateRoleRequestDTO
    {
        [Required]
        [RegularExpression("^(Admin|User)$", ErrorMessage = "Role harus berupa 'Admin' atau 'User'.")]
        public string Role { get; set; } = string.Empty;
    }
}
