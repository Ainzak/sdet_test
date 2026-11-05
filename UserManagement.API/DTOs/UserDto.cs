using System.ComponentModel.DataAnnotations;

namespace UserManagement.API.DTOs;

public sealed class UserDto
{
    [Required(ErrorMessage = "CountryIsoCode is required.")]
    [RegularExpression(@"\A[a-zA-Z]{2}\z", ErrorMessage = "CountryIsoCode must contain exactly two Latin letters.")]
    public string? CountryIsoCode { get; init; }

    [Required(ErrorMessage = "Locale is required.")]
    [RegularExpression(@"\A[a-z]{2}-[A-Z]{2}\z", ErrorMessage = "Locale must match xx-XX, for example ru-RU.")]
    public string? Locale { get; init; }
}
