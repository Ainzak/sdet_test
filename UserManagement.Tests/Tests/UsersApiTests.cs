using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

[TestClass]
public sealed class UsersApiTests : ApiTestBase
{
    [TestMethod]
    public async Task API_01_Create_ReturnsServerMetadataAndResolvableLocation()
    {
        var created = await CreateUser();
        Assert.AreEqual(created, await GetUser(created.UserId));
    }

    [TestMethod]
    [DataRow("ru", "ru-RU")]
    [DataRow("uS", "en-US")]
    [DataRow("GB", "en-GB")]
    [DataRow("zz", "zz-ZZ")]
    public async Task API_02_Country_IsNormalizedWithoutDictionaryValidation(string country, string locale)
    {
        var created = await CreateUser(country, locale);
        Assert.AreEqual(created, await GetUser(created.UserId));
    }

    [TestMethod]
    public async Task API_03_EndToEnd_CreateReadUpdateRead_PreservesCreationMetadata()
    {
        var created = await CreateUser();
        Assert.AreEqual(created, await GetUser(created.UserId));
        foreach (var (country, locale) in new[] { ("us", "en-US"), ("gB", "en-GB") })
        {
            var before = DateTime.UtcNow;
            using var response = await Client.PutAsJsonAsync($"/api/users/{created.UserId}",
                new { countryIsoCode = country, locale });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var updated = await ReadUser(response);
            Assert.AreEqual(created.UserId, updated.UserId);
            Assert.AreEqual(created.DateCreated, updated.DateCreated);
            Assert.AreEqual(country.ToUpperInvariant(), updated.CountryIsoCode);
            Assert.AreEqual(locale, updated.Locale);
            Assert.IsNotNull(updated.DateModified);
            Assert.IsTrue(updated.DateModified >= before && updated.DateModified <= DateTime.UtcNow);
            Assert.AreEqual(updated, await GetUser(created.UserId));
        }
    }

    [TestMethod]
    public async Task API_04_List_ReturnsEmptyThenAllUsersWithCorrectSchema()
    {
        Assert.IsEmpty(await GetUsers());
        var first = await CreateUser();
        var second = await CreateUser("us", "en-US");
        CollectionAssert.AreEqual(new[] { first, second }.OrderBy(user => user.DateCreated)
            .ThenBy(user => user.UserId).ToArray(), await GetUsers());
    }

    [TestMethod]
    public async Task API_05_SeparateApplications_DoNotShareRepository()
    {
        var created = await CreateUser();
        await using var otherFactory = new WebApplicationFactory<Program>();
        using var otherClient = otherFactory.CreateClient();
        using var response = await otherClient.GetAsync($"/api/users/{created.UserId}");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var list = await otherClient.GetFromJsonAsync<UserResponse[]>("/api/users");
        Assert.IsNotNull(list);
        Assert.IsEmpty(list);
        Assert.AreEqual(created, await GetUser(created.UserId));
    }
}
