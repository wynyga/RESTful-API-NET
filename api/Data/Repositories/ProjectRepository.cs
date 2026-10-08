using Microsoft.EntityFrameworkCore;
using Models;
using Models.Entities;
using Models.Queries;

namespace Data.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly AppDbContext _context;

        public ProjectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<Project> Items, int Total)> QueryAsync(ProjectFilter filter, CancellationToken ct = default)
        {
            IQueryable<Project> query = _context.Projects.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(p => p.ProjectName.Contains(term) || p.Description.Contains(term));
            }

            // "Overdue" is not stored: an unfinished project whose end date has passed.
            var today = filter.Today;
            query = filter.Status switch
            {
                ProjectStatus.Completed => query.Where(p => p.Status == ProjectStatus.Completed),
                ProjectStatus.Overdue => query.Where(p => p.Status != ProjectStatus.Completed && p.EndDate < today),
                ProjectStatus.OnProgress => query.Where(p => p.Status != ProjectStatus.Completed && p.EndDate >= today),
                _ => query,
            };

            var total = await query.CountAsync(ct);

            var ordered = (filter.SortBy, filter.Descending) switch
            {
                ("status", false) => query.OrderBy(p => p.Status),
                ("status", true) => query.OrderByDescending(p => p.Status),
                ("startDate", false) => query.OrderBy(p => p.StartDate),
                ("startDate", true) => query.OrderByDescending(p => p.StartDate),
                ("endDate", false) => query.OrderBy(p => p.EndDate),
                ("endDate", true) => query.OrderByDescending(p => p.EndDate),
                ("progress", false) => query.OrderBy(p => p.Progress),
                ("progress", true) => query.OrderByDescending(p => p.Progress),
                (_, true) => query.OrderByDescending(p => p.ProjectName),
                _ => query.OrderBy(p => p.ProjectName),
            };

            var items = await ordered
                .ThenBy(p => p.Id) // a stable order, so pages never overlap
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<ProjectSummary> GetSummaryAsync(DateTime today, CancellationToken ct = default)
        {
            var projects = _context.Projects.AsNoTracking();

            // Four small aggregate queries: every number is computed by the database, no rows are loaded.
            var total = await projects.CountAsync(ct);
            if (total == 0) return new ProjectSummary(0, 0, 0, 0);

            var completed = await projects.CountAsync(p => p.Status == ProjectStatus.Completed, ct);
            var overdue = await projects.CountAsync(p => p.Status != ProjectStatus.Completed && p.EndDate < today, ct);
            var average = await projects.AverageAsync(p => (double)p.Progress, ct);

            return new ProjectSummary(total, completed, overdue, average);
        }

        public async Task<Project?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Projects.FindAsync(new object[] { id }, ct);
        }

        public async Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
        {
            return await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectName == name, ct);
        }

        public Task<bool> AnyAsync(CancellationToken ct = default) => _context.Projects.AnyAsync(ct);

        public async Task<Project> AddAsync(Project project, CancellationToken ct = default)
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync(ct);
            return project;
        }

        public async Task UpdateAsync(Project project, CancellationToken ct = default)
        {
            _context.Projects.Update(project);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Project project, CancellationToken ct = default)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(ct);
        }
    }
}
