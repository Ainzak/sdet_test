using UserManagement.API.Models;

namespace UserManagement.API.Interfaces;

public interface IUserRepository
{
    User Add(User user);
    bool Update(User user);
    User? GetById(Guid userId);
    IEnumerable<User> GetAll();
}
