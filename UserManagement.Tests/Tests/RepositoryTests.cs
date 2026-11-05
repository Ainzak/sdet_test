using UserManagement.API.Models;
using UserManagement.API.Repositories;

namespace UserManagement.Tests.Tests;

[TestClass]
public sealed class RepositoryTests
{
    [TestMethod]
    public void REP_01_Repository_DoesNotExposeStoredObjectsOrLiveEnumeration()
    {
        var repository = new InMemoryUserRepository();
        var original = NewUser();
        var expected = original with { };
        var added = repository.Add(original);
        original.Locale = "en-US";
        added.CountryIsoCode = "US";
        Assert.AreEqual(expected, repository.GetById(expected.UserId));

        var read = repository.GetById(expected.UserId)!;
        read.Locale = "en-GB";
        Assert.AreEqual(expected, repository.GetById(expected.UserId));

        var snapshot = repository.GetAll();
        var fromList = snapshot.Single();
        fromList.CountryIsoCode = "GB";
        Assert.AreEqual(expected, repository.GetById(expected.UserId));

        var updated = expected with { CountryIsoCode = "US", Locale = "en-US", DateModified = DateTime.UtcNow };
        var expectedUpdated = updated with { };
        Assert.IsTrue(repository.Update(updated));
        updated.Locale = "de-DE";
        Assert.AreEqual(expectedUpdated, repository.GetById(expected.UserId));
        repository.Add(NewUser());
        Assert.HasCount(1, snapshot.ToArray());
        Assert.AreEqual("ru-RU", fromList.Locale);
        Assert.HasCount(2, repository.GetAll().ToArray());
    }

    [TestMethod]
    public void REP_02_MissingUserAndUpdate_PreserveRepositoryContract()
    {
        var repository = new InMemoryUserRepository();
        var original = NewUser();
        Assert.IsNull(repository.GetById(original.UserId));
        Assert.IsFalse(repository.Update(original));
        Assert.IsEmpty(repository.GetAll());
        repository.Add(original);
        var update = original with
        {
            CountryIsoCode = "US", Locale = "en-US", DateCreated = DateTime.UnixEpoch,
            DateModified = DateTime.UtcNow
        };
        Assert.IsTrue(repository.Update(update));
        Assert.AreEqual(update with { DateCreated = original.DateCreated }, repository.GetById(original.UserId));
    }

    private static User NewUser() => new()
    {
        UserId = Guid.NewGuid(), CountryIsoCode = "RU", Locale = "ru-RU", DateCreated = DateTime.UtcNow
    };
}
