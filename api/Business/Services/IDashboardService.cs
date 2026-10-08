using Models.DTOs;

namespace Business.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDTO> GetDashboardSummaryAsync(CancellationToken ct = default);
    }
}
