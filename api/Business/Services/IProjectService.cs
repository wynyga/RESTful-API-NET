using Models.DTOs;

namespace Business.Services
{
    public interface IProjectService
    {
        Task<PagedResultDTO<ProjectResponseDTO>> GetProjectsAsync(ProjectQueryDTO query, CancellationToken ct = default);
        Task<ProjectResponseDTO?> GetProjectByIdAsync(string encryptedId, CancellationToken ct = default);

        /// <exception cref="InvalidOperationException">The project name is already taken.</exception>
        Task<ProjectResponseDTO?> CreateProjectAsync(ProjectRequestDTO request, CancellationToken ct = default);

        /// <exception cref="InvalidOperationException">The project name is already taken.</exception>
        Task<ProjectResponseDTO?> UpdateProjectAsync(string encryptedId, ProjectRequestDTO request, CancellationToken ct = default);

        Task<bool> DeleteProjectAsync(string encryptedId, CancellationToken ct = default);
    }
}
