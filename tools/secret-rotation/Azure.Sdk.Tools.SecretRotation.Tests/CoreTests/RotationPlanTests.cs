using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Tests.CoreTests;

public class RotationPlanTests
{
    /// <summary>
    ///     Given a rotation plan with a primary or secondary store having an expiration date after the rotation threshold
    ///     When GetStatusAsync is called
    ///     Then the result's Expired and ThresholdExpired properties should return False
    ///
    ///     Given a primary or secondary expiration date before or on the rotation threshold
    ///     When GetStatusAsync is called
    ///     Then the result's ThresholdExpired property should return True
    ///
    ///     Given a primary or secondary expiration date before or on the invocation instant
    ///     When GetStatusAsync is called
    ///     Then the result's Expired property should return True
    /// </summary>
    [Theory]
    [TestCase(24, 0, 30, true, true)]
    [TestCase(24, -1, 30, true, true)]
    [TestCase(24, -1, 23, true, true)]
    [TestCase(24, 30, 0, true, true)]
    [TestCase(24, 30, -1, true, true)]
    [TestCase(24, 23, -1, true, true)]
    [TestCase(24, 24, 30, false, true)]
    [TestCase(24, 23, 30, false, true)]
    [TestCase(24, 30, 24, false, true)]
    [TestCase(24, 30, 23, false, true)]
    [TestCase(24, 25, 25, false, false)]
    public async Task GetStatusAsync_ExpectExpirationState(
        int thresholdHours,
        int hoursUntilPrimaryExpires,
        int hoursUntilSecondaryExpires,
        bool expectExpired,
        bool expectThresholdExpired)
    {
        DateTimeOffset staticTestTime = DateTimeOffset.Parse("2020-06-01T12:00:00Z");
        TimeSpan threshold = TimeSpan.FromHours(thresholdHours);

        var primaryState =
            new SecretState { ExpirationDate = staticTestTime.AddHours(hoursUntilPrimaryExpires) }; // after threshold
        var secondaryState =
            new SecretState
            {
                ExpirationDate = staticTestTime.AddHours(hoursUntilSecondaryExpires)
            }; // before threshold

        var rotationPlan = new RotationPlan(
            Mock.Of<ILogger>(),
            Mock.Of<TimeProvider>(x => x.GetCurrentDateTimeOffset() == staticTestTime),
            "TestPlan",
            Mock.Of<SecretStore>(),
            Mock.Of<SecretStore>(x => x.GetCurrentStateAsync() == Task.FromResult(primaryState)),
            new[]
            {
                Mock.Of<SecretStore>(x => x.CanRead && x.GetCurrentStateAsync() == Task.FromResult(secondaryState))
            },
            threshold,
            default,
            default);

        // Act
        RotationPlanStatus status = await rotationPlan.GetStatusAsync();

        Assert.AreEqual(expectExpired, status.Expired);
        Assert.AreEqual(expectThresholdExpired, status.ThresholdExpired);
    }

    /// <summary>
    ///     Given a rotation artifact with a RevokeAfterDate after than the invocation time
    ///     When GetStatusAsync is called
    ///     Then the result's RequiresRevocation property should return False
    ///
    ///     Given a rotation plan with a RevokeAfterDate before or on the invocation time
    ///     When GetStatusAsync is called
    ///     Then the result's RequiresRevocation property should return True
    /// </summary>
    [Test]
    [TestCase(1, false)]
    [TestCase(0, true)]
    [TestCase(-1, true)]
    public async Task GetStatusAsync_RequiresRevocation(int hoursUntilRevocation, bool expectRequiresRevocation)
    {
        DateTimeOffset staticTestTime = DateTimeOffset.Parse("2020-06-01T12:00:00Z");

        var rotationArtifacts = new[]
        {
            new SecretState { RevokeAfterDate = staticTestTime.AddHours(hoursUntilRevocation) } // not yet revokable
        };

        var rotationPlan = new RotationPlan(
            Mock.Of<ILogger>(),
            Mock.Of<TimeProvider>(x => x.GetCurrentDateTimeOffset() == staticTestTime),
            "TestPlan",
            Mock.Of<SecretStore>(),
            Mock.Of<SecretStore>(
                x => x.GetCurrentStateAsync() == Task.FromResult(new SecretState()) &&
                     x.GetRotationArtifactsAsync() == Task.FromResult(rotationArtifacts.AsEnumerable())),
            Array.Empty<SecretStore>(),
            default,
            default,
            default);

        // Act
        RotationPlanStatus status = await rotationPlan.GetStatusAsync();

        Assert.AreEqual(expectRequiresRevocation, status.RequiresRevocation);
    }

    /// <summary>
    ///     Given a rotation plan with:
    ///       A primary store with a rotation artifact requiring revocation
    ///       A secondary store with an action to perform during revocation
    ///     When ExecuteAsync is called on the plan
    ///     Then the secondary store's revocation action should be invoked
    /// </summary>
    [Test]
    public async Task RotatePlansAsync_RequiresRevocation_DoesRevocation()
    {
        DateTimeOffset staticTestTime = DateTimeOffset.Parse("2020-06-01T12:00:00Z");

        var primaryState = new SecretState { ExpirationDate = staticTestTime.AddDays(5) }; // after threshold

        var rotationArtifacts = new[]
        {
            new SecretState
            {
                RevokeAfterDate = staticTestTime.AddDays(-1), Tags = { ["value"] = "OldValue" }
            } // revokable
        };

        string externalState = "NotRevoked";

        Func<Task> revocationAction = () =>
        {
            externalState = "Revoked";
            return Task.CompletedTask;
        };

        var originStore = Mock.Of<SecretStore>();

        var primaryStore = Mock.Of<SecretStore>(x =>
            x.GetCurrentStateAsync() == Task.FromResult(primaryState) &&
            x.GetRotationArtifactsAsync() == Task.FromResult(rotationArtifacts.AsEnumerable()));

        var secondaryStore = Mock.Of<SecretStore>(x =>
            x.CanRevoke &&
            x.GetRevocationActionAsync(It.IsAny<SecretState>(), It.IsAny<bool>()) == revocationAction);

        var rotationPlan = new RotationPlan(
            Mock.Of<ILogger>(),
            Mock.Of<TimeProvider>(x => x.GetCurrentDateTimeOffset() == staticTestTime),
            "TestPlan",
            originStore,
            primaryStore,
            new[] { secondaryStore },
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(2),
            default);

        // Act
        await rotationPlan.ExecuteAsync(true, false);

        Assert.AreEqual("Revoked", externalState);
    }
}
