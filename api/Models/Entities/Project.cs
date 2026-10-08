using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Entities
{
    [Table("Projects")]
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string ProjectName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "On Progress";

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(0, 100)]
        public int Progress { get; set; }
    }
}
