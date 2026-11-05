using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UserManagement.Tests.Infrastructure;

public abstract class ApiTestBase
{
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        Factory = new WebApplicationFactory<Program>();
        Client = Factory.CreateClient();
        Client.Timeout = TimeSpan.FromSeconds(15);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    protected async Task<UserResponse> CreateUser(string country = "ru", string locale = "ru-RU")
    {
        var before = DateTime.UtcNow;
        using var response = await Client.PostAsJsonAsync("/api/users", new { countryIsoCode = country, locale });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var user = await ReadUser(response);
        Assert.AreEqual(country.ToUpperInvariant(), user.CountryIsoCode);
        Assert.AreEqual(locale, user.Locale);
        Assert.IsNull(user.DateModified);
        Assert.IsTrue(user.DateCreated >= before && user.DateCreated <= DateTime.UtcNow);
        Assert.IsNotNull(response.Headers.Location);
        Assert.AreEqual($"/api/users/{user.UserId}", response.Headers.Location.AbsolutePath);
        return user;
    }

    protected async Task<UserResponse> GetUser(Guid id)
    {
        using var response = await Client.GetAsync($"/api/users/{id}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return await ReadUser(response);
    }

    protected async Task<UserResponse[]> GetUsers(string query = "")
    {
        using var response = await Client.GetAsync($"/api/users{query}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            AssertUserSchema(element);
        }

        return JsonSerializer.Deserialize<UserResponse[]>(json, JsonSerializerOptions.Web)!;
    }

    protected static async Task<UserResponse> ReadUser(HttpResponseMessage response)
    {
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        AssertUserSchema(document.RootElement);
        return JsonSerializer.Deserialize<UserResponse>(json, JsonSerializerOptions.Web)!;
    }

    private static void AssertUserSchema(JsonElement element)
    {
        Assert.AreEqual(JsonValueKind.Object, element.ValueKind);
        CollectionAssert.AreEquivalent(
            new[] { "userId", "countryIsoCode", "dateCreated", "dateModified", "locale" },
            element.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(JsonValueKind.String, element.GetProperty("userId").ValueKind);
        Assert.IsTrue(element.GetProperty("userId").TryGetGuid(out var id));
        Assert.AreNotEqual(Guid.Empty, id);
        StringAssert.Matches(element.GetProperty("countryIsoCode").GetString()!, new Regex(@"\A[A-Z]{2}\z"));
        StringAssert.Matches(element.GetProperty("locale").GetString()!, new Regex(@"\A[a-z]{2}-[A-Z]{2}\z"));
        var created = element.GetProperty("dateCreated").GetDateTime();
        Assert.AreEqual(DateTimeKind.Utc, created.Kind);
        Assert.IsTrue(created > DateTime.UnixEpoch);
        var modifiedElement = element.GetProperty("dateModified");
        if (modifiedElement.ValueKind != JsonValueKind.Null)
        {
            var modified = modifiedElement.GetDateTime();
            Assert.AreEqual(DateTimeKind.Utc, modified.Kind);
            Assert.IsTrue(modified >= created);
        }
    }

    protected static async Task AssertValidation(HttpResponseMessage response, string? field = null)
    {
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.AreEqual(400, root.GetProperty("status").GetInt32());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        var errors = root.GetProperty("errors").EnumerateObject().ToArray();
        Assert.IsNotEmpty(errors);
        Assert.IsTrue(errors.All(error => error.Value.GetArrayLength() > 0 &&
            error.Value.EnumerateArray().All(message => !string.IsNullOrWhiteSpace(message.GetString()))));
        if (field is not null)
        {
            Assert.IsTrue(errors.Any(error => error.Name.Equals(field, StringComparison.OrdinalIgnoreCase)),
                $"Missing validation error for {field}: {root}");
        }
    }

    protected async Task AssertInvalidBodyDoesNotChangeState(string? json, string? field = null)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/users"))
        {
            request.Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.SendAsync(request);
            await AssertValidation(response, field);
        }

        Assert.IsEmpty(await GetUsers());
        var original = await CreateUser();
        using (var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{original.UserId}"))
        {
            request.Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.SendAsync(request);
            await AssertValidation(response, field);
        }

        Assert.AreEqual(original, await GetUser(original.UserId));
        CollectionAssert.AreEqual(new[] { original }, await GetUsers());
    }
}

public sealed record UserResponse(
    Guid UserId,
    string CountryIsoCode,
    DateTime DateCreated,
    DateTime? DateModified,
    string Locale);
