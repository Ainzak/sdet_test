using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Repositories;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, User> _users = [];

    public User Add(User user)
    {
        lock (_sync)
        {
            _users.Add(user.UserId, user with { });
            return user with { };
        }
    }

    public bool Update(User user)
    {
        lock (_sync)
        {
            if (!_users.TryGetValue(user.UserId, out var existing))
            {
                return false;
            }

            _users[user.UserId] = user with { DateCreated = existing.DateCreated };
            return true;
        }
    }

    public User? GetById(Guid userId)
    {
        lock (_sync)
        {
            return _users.TryGetValue(userId, out var user) ? user with { } : null;
        }
    }

    public IEnumerable<User> GetAll()
    {
        lock (_sync)
        {
            return _users.Values.Select(user => user with { }).ToArray();
        }
    }
}
