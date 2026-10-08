using Models.Entities;

namespace Data.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(int id);
        Task<User> AddUserAsync(User user);
        Task<List<User>> GetAllUsersAsync();
        Task UpdateUserRoleAsync(int id, string role);
    }
}

