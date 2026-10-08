using System.ComponentModel.DataAnnotations;

namespace Models.DTOs
{
    public class ProjectRequestDTO : IValidatableObject
    {
        [Required]
        [StringLength(255)]
        public string ProjectName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// "Overdue" is accepted for compatibility but is derived from the end date, so it is stored as "On Progress".
        /// </summary>
        [Required]
        [RegularExpression(ProjectStatus.Pattern, ErrorMessage = "Status harus berupa 'On Progress', 'Completed', atau 'Overdue'.")]
        public string Status { get; set; } = ProjectStatus.OnProgress;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(0, 100, ErrorMessage = "Progress harus berada dalam rentang 0 hingga 100.")]
        public int Progress { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult("End Date tidak boleh lebih awal dari Start Date.", new[] { nameof(EndDate) });
            }

            // Status and progress must tell the same story.
            if (Status == ProjectStatus.Completed && Progress != 100)
            {
                yield return new ValidationResult("Project berstatus 'Completed' harus memiliki Progress 100.", new[] { nameof(Progress) });
            }
            else if (Status != ProjectStatus.Completed && Progress == 100)
            {
                yield return new ValidationResult("Progress 100 berarti project harus berstatus 'Completed'.", new[] { nameof(Status) });
            }
        }
    }
}
