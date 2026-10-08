using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Business.Services;
using Data.Repositories;
using Models.DTOs;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IEncryptionService _encryptionService;

        public UsersController(IUserRepository userRepository, IEncryptionService encryptionService)
        {
            _userRepository = userRepository;
            _encryptionService = encryptionService;
        }

        [HttpPut("{encryptedId}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(string encryptedId, [FromBody] UpdateRoleRequestDTO request)
        {

            try
            {
                int userId = _encryptionService.DecryptId(encryptedId);
                var user = await _userRepository.GetUserByIdAsync(userId);
                
                if (user == null)
                {
                    return Problem(detail: "User tidak ditemukan.", statusCode: StatusCodes.Status404NotFound);
                }

                await _userRepository.UpdateUserRoleAsync(userId, request.Role);
                return Ok(new { Message = $"Role user {user.Email} berhasil diupdate menjadi {request.Role}." });
            }
            catch (ArgumentException)
            {
                return Problem(detail: "ID User tidak valid.", statusCode: StatusCodes.Status400BadRequest);
            }
        }
    }
}
