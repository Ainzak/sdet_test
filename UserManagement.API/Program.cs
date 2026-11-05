using System.Text.Json.Serialization;
using UserManagement.API.Interfaces;
using UserManagement.API.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var app = builder.Build();
app.MapControllers();
app.Run();

// Makes the top-level entry point accessible to WebApplicationFactory.
public partial class Program;
