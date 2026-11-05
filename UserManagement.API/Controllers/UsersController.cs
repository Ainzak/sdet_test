using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UserManagement.API.DTOs;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserRepository repository) : ControllerBase
{
    [HttpPost]
    public ActionResult<User> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), Required] UserDto dto)
    {
        var user = repository.Add(new User
        {
            UserId = Guid.NewGuid(),
            CountryIsoCode = dto.CountryIsoCode!.ToUpperInvariant(),
            Locale = dto.Locale!,
            DateCreated = DateTime.UtcNow
        });

        return CreatedAtAction(nameof(GetById), new { userId = user.UserId }, user);
    }

    // No :guid route constraint: invalid GUIDs must produce 400 rather than 404.
    [HttpGet("{userId}")]
    public ActionResult<User> GetById(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return InvalidUserId();
        }

        var user = repository.GetById(userId);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{userId}")]
    public ActionResult<User> Update(Guid userId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), Required] UserDto dto)
    {
        if (userId == Guid.Empty)
        {
            return InvalidUserId();
        }

        var existing = repository.GetById(userId);
        if (existing is null)
        {
            return NotFound();
        }

        var updated = existing with
        {
            CountryIsoCode = dto.CountryIsoCode!.ToUpperInvariant(),
            Locale = dto.Locale!,
            DateModified = DateTime.UtcNow
        };

        return repository.Update(updated) ? Ok(updated) : NotFound();
    }

    [HttpGet]
    public ActionResult<IEnumerable<User>> GetAll([FromQuery] PaginationQuery query)
    {
        // Nullable integers otherwise bind an explicitly empty value as null.
        foreach (var name in new[] { "page", "pageSize" })
        {
            if (Request.Query.TryGetValue(name, out var values) &&
                values.Any(string.IsNullOrWhiteSpace))
            {
                ModelState.AddModelError(name, $"{name} must be a positive integer.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var users = repository.GetAll()
            .OrderBy(user => user.DateCreated)
            .ThenBy(user => user.UserId)
            .ToArray();

        if (query.Page is null && query.PageSize is null)
        {
            return Ok(users);
        }

        var pageSize = query.PageSize ?? 10;
        var offset = ((long)(query.Page ?? 1) - 1) * pageSize;
        return Ok(offset >= users.Length
            ? Array.Empty<User>()
            : users.Skip((int)offset).Take(pageSize).ToArray());
    }

    private ActionResult InvalidUserId()
    {
        ModelState.AddModelError("userId", "UserId must be a non-empty GUID.");
        return ValidationProblem(ModelState);
    }
}
