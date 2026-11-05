using System.Net;
using System.Net.Http.Json;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

[TestClass]
public sealed class ConcurrencyTests : ApiTestBase
{
    [TestMethod]
    public async Task CON_01_ParallelCreates_PreserveEveryUserWithUniqueId()
    {
        const int count = 40;
        var created = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => Task.Run(() => CreateUser())));
        var all = await GetUsers();
        Assert.HasCount(count, all);
        Assert.AreEqual(count, all.Select(user => user.UserId).Distinct().Count());
        CollectionAssert.AreEquivalent(created, all);
    }

    [TestMethod]
    public async Task CON_02_ParallelUpdatesAndReads_NeverExposeMixedFields()
    {
        var original = await CreateUser();
        var pairs = new[] { (Country: "RU", Locale: "ru-RU"), (Country: "US", Locale: "en-US"),
            (Country: "GB", Locale: "en-GB") };
        var operations = Enumerable.Range(0, 60).Select(index => Task.Run(async () =>
        {
            UserResponse user;
            if (index % 3 == 0)
            {
                user = await GetUser(original.UserId);
            }
            else
            {
                var pair = pairs[index % pairs.Length];
                using var response = await Client.PutAsJsonAsync($"/api/users/{original.UserId}",
                    new { countryIsoCode = pair.Country.ToLowerInvariant(), locale = pair.Locale });
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                user = await ReadUser(response);
                Assert.AreEqual(pair.Country, user.CountryIsoCode);
                Assert.AreEqual(pair.Locale, user.Locale);
                Assert.IsNotNull(user.DateModified);
            }

            Assert.AreEqual(original.UserId, user.UserId);
            Assert.AreEqual(original.DateCreated, user.DateCreated);
            Assert.IsTrue(pairs.Contains((user.CountryIsoCode, user.Locale)));
        }));

        await Task.WhenAll(operations);
        var final = await GetUser(original.UserId);
        Assert.IsTrue(pairs.Contains((final.CountryIsoCode, final.Locale)));
        Assert.IsNotNull(final.DateModified);
        Assert.HasCount(1, await GetUsers());
    }

    [TestMethod]
    public async Task CON_03_ParallelCreatesAndLists_ReturnCoherentSnapshots()
    {
        var operations = Enumerable.Range(0, 40).Select(index => Task.Run(async () =>
        {
            if (index % 2 == 0)
            {
                await CreateUser("us", "en-US");
            }
            else
            {
                var snapshot = await GetUsers();
                Assert.AreEqual(snapshot.Length, snapshot.Select(user => user.UserId).Distinct().Count());
                Assert.IsTrue(snapshot.All(user => user.CountryIsoCode == "US" && user.Locale == "en-US"));
            }
        }));

        await Task.WhenAll(operations);
        Assert.HasCount(20, await GetUsers());
    }
}
