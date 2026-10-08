using System.ComponentModel.DataAnnotations;

namespace Models.DTOs
{
    /// <summary>Query string of GET /api/projects.</summary>
    public class ProjectQueryDTO
    {
        [StringLength(100)]
        public string? Search { get; set; }

        [RegularExpression(ProjectStatus.Pattern, ErrorMessage = "Status harus berupa 'On Progress', 'Completed', atau 'Overdue'.")]
        public string? Status { get; set; }

        [RegularExpression("^(name|status|startDate|endDate|progress)$", ErrorMessage = "SortBy harus salah satu dari: name, status, startDate, endDate, progress.")]
        public string SortBy { get; set; } = "name";

        [RegularExpression("^(asc|desc)$", ErrorMessage = "SortOrder harus 'asc' atau 'desc'.")]
        public string SortOrder { get; set; } = "asc";

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
