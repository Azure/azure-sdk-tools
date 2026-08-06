using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class UserProfileTests
{
    [Test]
    public void UserProfile_SerializesAndDeserializes_UsingExpectedJsonShape()
    {
        const string json = """
        {"github":{"id":1111,"login":"abc","organizations":["abcOrg"]},"aad":{"alias":"testuser","preferredName":"Test User","userPrincipalName":"test@abc.com","emailAddress":"Test.User.@abc.com","id":"44444-3333-4444-5555-565554a062"}}
        """;

        var profile = JsonSerializer.Deserialize<UserProfile>(json);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.GitHub.Id, Is.EqualTo(1111));
        Assert.That(profile.GitHub.Login, Is.EqualTo("abc"));
        Assert.That(profile.GitHub.Organizations, Is.EqualTo(new[] { "abcOrg" }));
        Assert.That(profile.Aad.Alias, Is.EqualTo("testuser"));
        Assert.That(profile.Aad.PreferredName, Is.EqualTo("Test User"));
        Assert.That(profile.Aad.UserPrincipalName, Is.EqualTo("test@abc.com"));
        Assert.That(profile.Aad.EmailAddress, Is.EqualTo("Test.User.@abc.com"));
        Assert.That(profile.Aad.Id, Is.EqualTo("44444-3333-4444-5555-565554a062"));
    }
}