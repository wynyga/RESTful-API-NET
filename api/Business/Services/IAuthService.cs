using Models.DTOs;

namespace Business.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDTO?> LoginAsync(LoginRequestDTO request, CancellationToken ct = default);
        Task<UserProfileDTO?> GetProfileAsync(int userId, CancellationToken ct = default);

        /// <returns>The new profile, or null when the email is already registered.</returns>
        Task<UserProfileDTO?> RegisterAsync(RegisterRequestDTO request, CancellationToken ct = default);
    }
}
