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
    [TestCase(24, 22, 0, 30, RotationState.Expired)] // primary at expiration
    [TestCase(24, 22, -1, 30, RotationState.Expired)] // primary past expiration
    [TestCase(24, 22, -1, 23, RotationState.Expired)] // primary past expiration, secondary past rotation
    [TestCase(24, 22, 30, 0, RotationState.Expired)] // secondary at expiration
    [TestCase(24, 22, 30, -1, RotationState.Expired)] // secondary past expiration
    [TestCase(24, 22, 23, -1, RotationState.Expired)] // secondary past expiration, primary past rotation
    [TestCase(24, 22, 24, 30, RotationState.Rotate)] // primary at rotation
    [TestCase(24, 22, 23, 30, RotationState.Rotate)] // primary between rotate and warning
    [TestCase(24, 22, 30, 24, RotationState.Rotate)] // secondary at rotate
    [TestCase(24, 22, 30, 23, RotationState.Rotate)] // secondary between rotate and warning
    [TestCase(24, 22, 22, 30, RotationState.Warning)] // primary at warning
    [TestCase(24, 22, 10, 30, RotationState.Warning)] // primary past warning
    [TestCase(24, 22, 30, 22, RotationState.Warning)] // secondary at warning
    [TestCase(24, 22, 30, 10, RotationState.Warning)] // secondary past warning
    [TestCase(24, null, 30, 10, RotationState.Warning)] // implicit 12h warning window
    [TestCase(24, 22, 25, 25, RotationState.UpToDate)]
    public async Task GetStatusAsync_ExpectExpirationState(
        int rotateThresholdHours,
        int? warningThresholdHours,
        int hoursUntilPrimaryExpires,
        int hoursUntilSecondaryExpires,
        RotationState expectedState)
    {
        DateTimeOffset staticTestTime = DateTimeOffset.Parse("2020-06-01T12:00:00Z");
        TimeSpan rotateThreshold = TimeSpan.FromHours(rotateThresholdHours);
        TimeSpan? warningThreshold = warningThresholdHours.HasValue ? TimeSpan.FromHours(warningThresholdHours.Value) : null;

        var primaryState = new SecretState
        {
            ExpirationDate = staticTestTime.AddHours(hoursUntilPrimaryExpires)
        }; 

        var secondaryState = new SecretState
        {
            ExpirationDate = staticTestTime.AddHours(hoursUntilSecondaryExpires)
        };

        var rotationPlan = new RotationPlan(
            Mock.Of<ILogger>(),
            Mock.Of<TimeProvider>(x => x.GetUtcNow() == staticTestTime),
            "TestPlan",
            Mock.Of<SecretStore>(),
            Mock.Of<SecretStore>(x => x.GetCurrentStateAsync() == Task.FromResult(primaryState)),
            new[]
            {
                Mock.Of<SecretStore>(x => x.CanRead && x.GetCurrentStateAsync() == Task.FromResult(secondaryState))
            },
            rotateThreshold,
            warningThreshold,
            default,
            default);

        // Act
        RotationPlanStatus status = await rotationPlan.GetStatusAsync();

        Assert.AreEqual(expectedState, status.State);
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
            Mock.Of<TimeProvider>(x => x.GetUtcNow() == staticTestTime),
            "TestPlan",
            Mock.Of<SecretStore>(),
            Mock.Of<SecretStore>(
                x => x.GetCurrentStateAsync() == Task.FromResult(new SecretState()) &&
                     x.GetRotationArtifactsAsync() == Task.FromResult(rotationArtifacts.AsEnumerable())),
            Array.Empty<SecretStore>(),
            default,
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
            Mock.Of<TimeProvider>(x => x.GetUtcNow() == staticTestTime),
            "TestPlan",
            originStore,
            primaryStore,
            new[] { secondaryStore },
            rotationThreshold: TimeSpan.FromDays(3),
            warningThreshold: TimeSpan.FromDays(2),
            rotationPeriod: TimeSpan.FromDays(2),
            revokeAfterPeriod: default);

        // Act
        await rotationPlan.ExecuteAsync(true, false);

        Assert.AreEqual("Revoked", externalState);
    }
}
