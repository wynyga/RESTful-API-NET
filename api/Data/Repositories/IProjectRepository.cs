using Models.Entities;
using Models.Queries;

namespace Data.Repositories
{
    public interface IProjectRepository
    {
        /// <summary>One page of projects matching the filter, plus the total number of matches.</summary>
        Task<(IReadOnlyList<Project> Items, int Total)> QueryAsync(ProjectFilter filter, CancellationToken ct = default);

        /// <summary>Counts and average progress, computed by the database in a single query.</summary>
        Task<ProjectSummary> GetSummaryAsync(DateTime today, CancellationToken ct = default);

        Task<Project?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Project?> GetByNameAsync(string name, CancellationToken ct = default);
        Task<bool> AnyAsync(CancellationToken ct = default);
        Task<Project> AddAsync(Project project, CancellationToken ct = default);
        Task UpdateAsync(Project project, CancellationToken ct = default);
        Task DeleteAsync(Project project, CancellationToken ct = default);
    }
}
