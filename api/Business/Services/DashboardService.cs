using Data.Repositories;
using Models.DTOs;

namespace Business.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly TimeProvider _time;

        public DashboardService(IProjectRepository projectRepository, TimeProvider time)
        {
            _projectRepository = projectRepository;
            _time = time;
        }

        public async Task<DashboardSummaryDTO> GetDashboardSummaryAsync(CancellationToken ct = default)
        {
            var today = _time.GetUtcNow().UtcDateTime.Date;
            var summary = await _projectRepository.GetSummaryAsync(today, ct);

            return new DashboardSummaryDTO
            {
                TotalProject = summary.Total,
                OnProgress = summary.OnProgress,
                Completed = summary.Completed,
                Overdue = summary.Overdue,
                ProgressPercentage = Math.Round(summary.AverageProgress, 2),
            };
        }
    }
}
