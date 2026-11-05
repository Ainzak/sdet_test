using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests;

[TestClass]
public sealed class ValidationTests : ApiTestBase
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("1A")]
    [DataRow("R!")]
    [DataRow("R")]
    [DataRow("RUS")]
    [DataRow("РФ")]
    [DataRow("éU")]
    [DataRow(" RU")]
    [DataRow("RU ")]
    [DataRow("RU\n")]
    public async Task VAL_01_VAL_05_InvalidCountry_IsRejectedByPostAndPutWithoutMutation(string? value)
    {
        await AssertInvalidBodyDoesNotChangeState(
            JsonSerializer.Serialize(new { countryIsoCode = value, locale = "ru-RU" }), "CountryIsoCode");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("r1-RU")]
    [DataRow("ru-R!")]
    [DataRow("r-RU")]
    [DataRow("rus-RUS")]
    [DataRow("ru_RU")]
    [DataRow("RU-ru")]
    [DataRow("RU-RU")]
    [DataRow("ru-ru")]
    [DataRow("ру-РУ")]
    [DataRow(" ru-RU")]
    [DataRow("ru-RU ")]
    [DataRow("ru-RU\n")]
    public async Task VAL_02_VAL_05_InvalidLocale_IsRejectedByPostAndPutWithoutMutation(string? value)
    {
        await AssertInvalidBodyDoesNotChangeState(
            JsonSerializer.Serialize(new { countryIsoCode = "RU", locale = value }), "Locale");
    }

    [TestMethod]
    [DataRow("CountryIsoCode", "{\"locale\":\"ru-RU\"}")]
    [DataRow("Locale", "{\"countryIsoCode\":\"RU\"}")]
    public async Task VAL_01_VAL_02_VAL_05_MissingField_IsRejectedWithoutMutation(string field, string json)
    {
        await AssertInvalidBodyDoesNotChangeState(json, field);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"countryIsoCode\":123,\"locale\":\"ru-RU\"}")]
    [DataRow("{\"countryIsoCode\":\"RU\",\"locale\":false}")]
    public async Task VAL_03_VAL_05_InvalidBody_IsRejectedWithoutMutation(string? json)
    {
        await AssertInvalidBodyDoesNotChangeState(json);
    }

    [TestMethod]
    [DataRow("unknown", "123")]
    [DataRow("UserId", "\"11111111-1111-1111-1111-111111111111\"")]
    [DataRow("userId", "\"11111111-1111-1111-1111-111111111111\"")]
    [DataRow("DateCreated", "\"2000-01-01T00:00:00Z\"")]
    [DataRow("dateCreated", "\"2000-01-01T00:00:00Z\"")]
    [DataRow("DateModified", "null")]
    [DataRow("dateModified", "null")]
    public async Task VAL_04_VAL_05_UnknownAndServerFields_AreRejectedWithoutMutation(string field, string jsonValue)
    {
        await AssertInvalidBodyDoesNotChangeState(
            $$"""{"countryIsoCode":"RU","locale":"ru-RU","{{field}}":{{jsonValue}}}""");
    }

    [TestMethod]
    [DataRow("not-a-guid")]
    [DataRow("123")]
    [DataRow("11111111-1111-1111-1111-11111111111g")]
    [DataRow("00000000-0000-0000-0000-000000000000")]
    public async Task ID_01_InvalidGuid_Returns400ForGetAndPut(string id)
    {
        using var get = await Client.GetAsync($"/api/users/{id}");
        await AssertValidation(get, "userId");
        using var put = await Client.PutAsJsonAsync($"/api/users/{id}",
            new { countryIsoCode = "RU", locale = "ru-RU" });
        await AssertValidation(put, "userId");
        Assert.IsEmpty(await GetUsers());
    }

    [TestMethod]
    public async Task ID_02_UnknownGuid_Returns404ForGetAndPutWithoutCreatingUser()
    {
        var id = Guid.NewGuid();
        using var get = await Client.GetAsync($"/api/users/{id}");
        Assert.AreEqual(HttpStatusCode.NotFound, get.StatusCode);
        using var put = await Client.PutAsJsonAsync($"/api/users/{id}",
            new { countryIsoCode = "RU", locale = "ru-RU" });
        Assert.AreEqual(HttpStatusCode.NotFound, put.StatusCode);
        Assert.IsEmpty(await GetUsers());
    }
}
