using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Services;
using Data.Repositories;
using Models.DTOs;
using System.Linq;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IEncryptionService _encryptionService;

        public AdminController(IUserRepository userRepository, IEncryptionService encryptionService)
        {
            _userRepository = userRepository;
            _encryptionService = encryptionService;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userRepository.GetAllUsersAsync();
            var userList = users.Select(u => new UserListDTO
            {
                Id = _encryptionService.EncryptId(u.Id),
                Email = u.Email,
                Role = u.Role
            }).ToList();

            return Ok(userList);
        }
    }
}
