namespace UserManagement.API.Models;

public sealed record User
{
    public Guid UserId { get; set; }
    public required string CountryIsoCode { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public required string Locale { get; set; }
}
