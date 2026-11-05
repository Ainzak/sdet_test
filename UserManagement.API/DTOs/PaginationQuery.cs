using System.ComponentModel.DataAnnotations;

namespace UserManagement.API.DTOs;

public sealed class PaginationQuery
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be a positive integer.")]
    public int? Page { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "PageSize must be a positive integer.")]
    public int? PageSize { get; init; }
}
