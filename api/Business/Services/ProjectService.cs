using Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Models.DTOs;
using Models.Entities;
using Models.Queries;

namespace Business.Services
{
    public class ProjectService : IProjectService
    {
        private const string DuplicateName = "Nama project sudah digunakan.";

        private readonly IProjectRepository _projectRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly TimeProvider _time;

        public ProjectService(IProjectRepository projectRepository, IEncryptionService encryptionService, TimeProvider time)
        {
            _projectRepository = projectRepository;
            _encryptionService = encryptionService;
            _time = time;
        }

        private DateTime Today => _time.GetUtcNow().UtcDateTime.Date;

        private ProjectResponseDTO MapToDTO(Project project) => new()
        {
            Id = _encryptionService.EncryptId(project.Id),
            ProjectName = project.ProjectName,
            Description = project.Description,
            Status = ProjectStatusRules.Effective(project.Status, project.EndDate, Today),
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            Progress = project.Progress,
        };

        public async Task<PagedResultDTO<ProjectResponseDTO>> GetProjectsAsync(ProjectQueryDTO query, CancellationToken ct = default)
        {
            var filter = new ProjectFilter(
                query.Search, query.Status, query.SortBy, query.SortOrder == "desc", query.Page, query.PageSize, Today);

            var (items, total) = await _projectRepository.QueryAsync(filter, ct);

            return new PagedResultDTO<ProjectResponseDTO>
            {
                Items = items.Select(MapToDTO).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                Total = total,
            };
        }

        public async Task<ProjectResponseDTO?> GetProjectByIdAsync(string encryptedId, CancellationToken ct = default)
        {
            if (!_encryptionService.TryDecryptId(encryptedId, out var id)) return null;
            var project = await _projectRepository.GetByIdAsync(id, ct);
            return project is null ? null : MapToDTO(project);
        }

        public async Task<ProjectResponseDTO?> CreateProjectAsync(ProjectRequestDTO request, CancellationToken ct = default)
        {
            if (await _projectRepository.GetByNameAsync(request.ProjectName, ct) is not null)
            {
                throw new InvalidOperationException(DuplicateName);
            }

            var project = new Project
            {
                ProjectName = request.ProjectName,
                Description = request.Description,
                Status = ProjectStatusRules.ToStored(request.Status),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Progress = request.Progress,
            };

            try
            {
                project = await _projectRepository.AddAsync(project, ct);
            }
            catch (DbUpdateException)
            {
                // Two requests passed the check above at once and the unique index decided.
                if (await _projectRepository.GetByNameAsync(request.ProjectName, ct) is not null)
                {
                    throw new InvalidOperationException(DuplicateName);
                }
                throw;
            }
            return MapToDTO(project);
        }

        public async Task<ProjectResponseDTO?> UpdateProjectAsync(string encryptedId, ProjectRequestDTO request, CancellationToken ct = default)
        {
            if (!_encryptionService.TryDecryptId(encryptedId, out var id)) return null;
            var project = await _projectRepository.GetByIdAsync(id, ct);
            if (project is null) return null;

            if (project.ProjectName != request.ProjectName
                && await _projectRepository.GetByNameAsync(request.ProjectName, ct) is not null)
            {
                throw new InvalidOperationException(DuplicateName);
            }

            project.ProjectName = request.ProjectName;
            project.Description = request.Description;
            project.Status = ProjectStatusRules.ToStored(request.Status);
            project.StartDate = request.StartDate;
            project.EndDate = request.EndDate;
            project.Progress = request.Progress;

            try
            {
                await _projectRepository.UpdateAsync(project, ct);
            }
            catch (DbUpdateException)
            {
                if (await _projectRepository.GetByNameAsync(request.ProjectName, ct) is { } other && other.Id != project.Id)
                {
                    throw new InvalidOperationException(DuplicateName);
                }
                throw;
            }
            return MapToDTO(project);
        }

        public async Task<bool> DeleteProjectAsync(string encryptedId, CancellationToken ct = default)
        {
            if (!_encryptionService.TryDecryptId(encryptedId, out var id)) return false;
            var project = await _projectRepository.GetByIdAsync(id, ct);
            if (project is null) return false;

            await _projectRepository.DeleteAsync(project, ct);
            return true;
        }
    }
}
