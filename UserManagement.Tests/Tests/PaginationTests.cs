using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

[TestClass]
public sealed class PaginationTests : ApiTestBase
{
    [TestMethod]
    public async Task PAGE_01_PAGE_02_AllAndExplicitPages_HaveNoGapsOrDuplicates()
    {
        var created = new List<UserResponse>();
        for (var index = 0; index < 23; index++)
        {
            created.Add(await CreateUser());
        }

        var expected = created.OrderBy(user => user.DateCreated).ThenBy(user => user.UserId).ToArray();
        CollectionAssert.AreEqual(expected, await GetUsers());
        var combined = new List<UserResponse>();
        for (var page = 1; page <= 5; page++)
        {
            var actual = await GetUsers($"?page={page}&pageSize=5");
            CollectionAssert.AreEqual(expected.Skip((page - 1) * 5).Take(5).ToArray(), actual);
            combined.AddRange(actual);
        }

        CollectionAssert.AreEqual(expected, combined.ToArray());
        Assert.AreEqual(23, combined.Select(user => user.UserId).Distinct().Count());
    }

    [TestMethod]
    [DataRow("?page=1", 0, 10)]
    [DataRow("?page=2", 10, 2)]
    [DataRow("?pageSize=3", 0, 3)]
    public async Task PAGE_03_MissingParameter_UsesDocumentedDefault(string query, int skip, int take)
    {
        for (var index = 0; index < 12; index++)
        {
            await CreateUser();
        }

        var all = await GetUsers();
        CollectionAssert.AreEqual(all.Skip(skip).Take(take).ToArray(), await GetUsers(query));
    }

    [TestMethod]
    public async Task PAGE_04_EmptyAndOutOfRangePages_ReturnEmptyArrays()
    {
        Assert.IsEmpty(await GetUsers("?page=1&pageSize=5"));
        await CreateUser();
        Assert.IsEmpty(await GetUsers("?page=2&pageSize=1"));
    }

    [TestMethod]
    [DataRow("page", "0")]
    [DataRow("page", "-1")]
    [DataRow("page", "1.5")]
    [DataRow("page", "abc")]
    [DataRow("page", "")]
    [DataRow("page", " ")]
    [DataRow("page", "2147483648")]
    [DataRow("pageSize", "0")]
    [DataRow("pageSize", "-1")]
    [DataRow("pageSize", "1.5")]
    [DataRow("pageSize", "abc")]
    [DataRow("pageSize", "")]
    [DataRow("pageSize", " ")]
    [DataRow("pageSize", "2147483648")]
    public async Task PAGE_05_InvalidParameter_Returns400(string parameter, string value)
    {
        using var response = await Client.GetAsync($"/api/users?{parameter}={Uri.EscapeDataString(value)}");
        await AssertValidation(response, parameter);
    }

    [TestMethod]
    public async Task PAGE_06_LargeValidIntegers_DoNotOverflow()
    {
        var created = await CreateUser();
        Assert.IsEmpty(await GetUsers("?page=2147483647&pageSize=2147483647"));
        Assert.IsEmpty(await GetUsers("?page=2147483647&pageSize=1"));
        CollectionAssert.AreEqual(new[] { created }, await GetUsers("?page=1&pageSize=2147483647"));
    }

    [TestMethod]
    public async Task PAGE_07_EqualCreationDates_UseGuidTieBreakerAndRemainStableAfterUpdate()
    {
        // Seed equal timestamps deliberately; HTTP-created timestamps need not collide.
        var repository = Factory.Services.GetRequiredService<IUserRepository>();
        var date = DateTime.UtcNow.AddMinutes(-1);
        var ids = new[]
        {
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("00000002-0000-0000-0000-000000000000")
        };
        foreach (var id in ids)
        {
            repository.Add(new User
            {
                UserId = id, CountryIsoCode = "RU", Locale = "ru-RU", DateCreated = date
            });
        }

        var expected = ids.Order().ToArray();
        CollectionAssert.AreEqual(expected, (await GetUsers()).Select(user => user.UserId).ToArray());
        CollectionAssert.AreEqual(expected.Take(2).ToArray(),
            (await GetUsers("?page=1&pageSize=2")).Select(user => user.UserId).ToArray());
        CollectionAssert.AreEqual(expected.Skip(2).ToArray(),
            (await GetUsers("?page=2&pageSize=2")).Select(user => user.UserId).ToArray());
        using var response = await Client.PutAsJsonAsync($"/api/users/{expected[0]}",
            new { countryIsoCode = "us", locale = "en-US" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(expected, (await GetUsers()).Select(user => user.UserId).ToArray());
        CollectionAssert.AreEqual(expected, (await GetUsers()).Select(user => user.UserId).ToArray());
    }
}
