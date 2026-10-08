using System.Security.Claims;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Models.DTOs;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO request, CancellationToken ct)
        {
            var response = await _authService.RegisterAsync(request, ct);
            if (response == null)
            {
                return Problem(detail: "Email sudah digunakan.", statusCode: StatusCodes.Status409Conflict);
            }

            return StatusCode(StatusCodes.Status201Created, new { Message = "Registrasi berhasil.", Data = response });
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request, CancellationToken ct)
        {
            var response = await _authService.LoginAsync(request, ct);
            if (response == null)
            {
                return Problem(detail: "Email atau password salah.", statusCode: StatusCodes.Status401Unauthorized);
            }

            return Ok(response);
        }

        [HttpGet("/api/profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var profile = await _authService.GetProfileAsync(userId, ct);
            if (profile == null)
            {
                return Problem(detail: "User tidak ditemukan.", statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(profile);
        }
    }
}
