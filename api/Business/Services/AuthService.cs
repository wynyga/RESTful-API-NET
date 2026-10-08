using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Data.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models.DTOs;
using Models.Entities;
using Models.Options;

namespace Business.Services
{
    public class AuthService : IAuthService
    {
        // Verified against when the email is unknown, so "no such user" and "wrong password"
        // take the same time and a caller cannot use the difference to discover accounts.
        private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

        private readonly IUserRepository _userRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly JwtSettings _jwt;
        private readonly TimeProvider _time;

        public AuthService(
            IUserRepository userRepository,
            IEncryptionService encryptionService,
            IOptions<JwtSettings> jwt,
            TimeProvider time)
        {
            _userRepository = userRepository;
            _encryptionService = encryptionService;
            _jwt = jwt.Value;
            _time = time;
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        public async Task<LoginResponseDTO?> LoginAsync(LoginRequestDTO request, CancellationToken ct = default)
        {
            var user = await _userRepository.GetUserByEmailAsync(NormalizeEmail(request.Email));

            var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
            if (user is null || !passwordOk) return null;

            var expires = _time.GetUtcNow().UtcDateTime.AddMinutes(_jwt.ExpirationInMinutes);
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                }),
                Expires = expires,
                Issuer = _jwt.Issuer,
                Audience = _jwt.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
                    SecurityAlgorithms.HmacSha256Signature),
            };

            var handler = new JwtSecurityTokenHandler();
            return new LoginResponseDTO
            {
                Token = handler.WriteToken(handler.CreateToken(descriptor)),
                ExpiresIn = _jwt.ExpirationInMinutes * 60,
            };
        }

        public async Task<UserProfileDTO?> GetProfileAsync(int userId, CancellationToken ct = default)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            return user is null ? null : ToProfile(user);
        }

        public async Task<UserProfileDTO?> RegisterAsync(RegisterRequestDTO request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            if (await _userRepository.GetUserByEmailAsync(email) is not null) return null;

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            };
            await _userRepository.AddUserAsync(user);
            return ToProfile(user);
        }

        private UserProfileDTO ToProfile(User user) => new()
        {
            Id = _encryptionService.EncryptId(user.Id),
            Email = user.Email,
            Role = user.Role,
        };
    }
}
