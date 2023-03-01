﻿using System.Collections.ObjectModel;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Core;

public class RotationPlan
{
    private readonly ILogger logger;
    private readonly TimeProvider timeProvider;

    public RotationPlan(ILogger logger,
        TimeProvider timeProvider,
        string name,
        SecretStore originStore,
        SecretStore primaryStore,
        IList<SecretStore> secondaryStores,
        TimeSpan rotationThreshold,
        TimeSpan rotationPeriod,
        TimeSpan? revokeAfterPeriod)
    {
        this.logger = logger;
        this.timeProvider = timeProvider;
        Name = name;
        OriginStore = originStore;
        PrimaryStore = primaryStore;
        RotationThreshold = rotationThreshold;
        RotationPeriod = rotationPeriod;
        RevokeAfterPeriod = revokeAfterPeriod;
        SecondaryStores = new ReadOnlyCollection<SecretStore>(secondaryStores);
    }

    public string Name { get; }

    public SecretStore OriginStore { get; }

    public SecretStore PrimaryStore { get; }

    public IReadOnlyCollection<SecretStore> SecondaryStores { get; }

    public TimeSpan RotationThreshold { get; }

    public TimeSpan RotationPeriod { get; }

    public TimeSpan? RevokeAfterPeriod { get; }

    public async Task ExecuteAsync(bool onlyRotateExpiring, bool whatIf)
    {
        string operationId = Guid.NewGuid().ToString();
        using IDisposable? loggingScope = this.logger.BeginScope(operationId);
        this.logger.LogInformation("\nProcessing rotation plan '{PlanName}'", Name);

        DateTimeOffset shouldRotateDate = this.timeProvider.GetCurrentDateTimeOffset().Add(RotationThreshold);

        this.logger.LogInformation("Getting current state of plan");

        if (onlyRotateExpiring)
        {
            this.logger.LogInformation("'{PlanName}' should be rotated if it expires on or before {ShouldRotateDate}",
                Name, shouldRotateDate);
        }

        SecretState currentState = await PrimaryStore.GetCurrentStateAsync();

        // TODO: Add secondary store state checks (detect drift)

        // any rotationPlan with an expiration date falling before now + threshold is "expiring"
        this.logger.LogInformation("'{PlanName}' expires on {ExpirationDate}.", Name, currentState.ExpirationDate);
        if (onlyRotateExpiring && currentState.ExpirationDate > shouldRotateDate)
        {
            this.logger.LogInformation(
                "Skipping rotation of plan '{PlanName}' because it expires after {ShouldRotateDate}", Name,
                shouldRotateDate);
        }
        else
        {
            await RotateAsync(operationId, currentState, whatIf);
        }

        await RevokeRotationArtifactsAsync(whatIf);
    }

    public async Task<RotationPlanStatus> GetStatusAsync()
    {
        DateTimeOffset invocationTime = this.timeProvider.GetCurrentDateTimeOffset();

        SecretState primaryStoreState = await PrimaryStore.GetCurrentStateAsync();

        IEnumerable<SecretState> rotationArtifacts = await PrimaryStore.GetRotationArtifactsAsync();

        var secondaryStoreStates = new List<SecretState>();

        foreach (SecretStore secondaryStore in SecondaryStores)
        {
            if (secondaryStore.CanRead)
            {
                secondaryStoreStates.Add(await secondaryStore.GetCurrentStateAsync());
            }
        }

        SecretState[] allStates = secondaryStoreStates.Prepend(primaryStoreState).ToArray();

        bool anyExpired = allStates.Any(state => state.ExpirationDate <= invocationTime);

        DateTimeOffset thresholdDate = this.timeProvider.GetCurrentDateTimeOffset().Add(RotationThreshold);

        bool anyThresholdExpired = allStates.Any(state => state.ExpirationDate <= thresholdDate);

        bool anyRequireRevocation = rotationArtifacts.Any(state => state.RevokeAfterDate <= invocationTime);

        var status = new RotationPlanStatus
        {
            Expired = anyExpired,
            ThresholdExpired = anyThresholdExpired,
            RequiresRevocation = anyRequireRevocation,
            PrimaryStoreState = primaryStoreState,
            SecondaryStoreStates = secondaryStoreStates.ToArray()
        };

        return status;
    }

    private async Task RotateAsync(string operationId, SecretState currentState, bool whatIf)
    {
        /*
         * General flow:
         *   Get a new secret value from origin
         *   Store the new value in all secondaries
         *   If origin != primary, store the secret in primary. Update of primary indicates completed rotation.
         *   If origin == primary, annotate origin to indicate complete. Annotation of origin indicates completed rotation.
         *     A user can combine origin and primary only when origin can be marked as complete in some way (e.g. Key Vault Certificates can be tagged)
         */

        DateTimeOffset invocationTime = this.timeProvider.GetCurrentDateTimeOffset();

        SecretValue newValue = await OriginateNewValueAsync(operationId, invocationTime, currentState, whatIf);

        await WriteValueToSecondaryStoresAsync(newValue, currentState, whatIf);

        await WriteValueToPrimaryAndOriginAsync(newValue, invocationTime, currentState, whatIf);
    }

    private async Task WriteValueToPrimaryAndOriginAsync(SecretValue newValue,
        DateTimeOffset invocationTime,
        SecretState currentState,
        bool whatIf)
    {
        DateTimeOffset? revokeAfterDate = RevokeAfterPeriod.HasValue
            ? invocationTime.Add(RevokeAfterPeriod.Value)
            : null;

        // Complete rotation by either annotating the origin or writing to primary
        if (OriginStore != PrimaryStore)
        {
            if (!PrimaryStore.CanWrite)
            {
                // Primary only has to support write when it's not also origin.
                throw new RotationException(
                    $"Rotation plan '{Name}' uses separate Primary and Origin stores, but its primary store type '{OriginStore.GetType()}' does not support CanWrite");
            }

            // New value along with the datetime when old values should be revoked
            await PrimaryStore.WriteSecretAsync(newValue, currentState, revokeAfterDate, whatIf);
        }
        else
        {
            if (!OriginStore.CanAnnotate)
            {
                throw new RotationException(
                    $"Rotation plan '{Name}' uses a combined Origin and Primary store, but the store type '{OriginStore.GetType()}' does not support CanAnnotate");
            }

            await OriginStore.MarkRotationCompleteAsync(newValue, revokeAfterDate, whatIf);
        }
    }

    private async Task WriteValueToSecondaryStoresAsync(SecretValue newValue, SecretState currentState, bool whatIf)
    {
        // TODO: some providers will issue secrets for longer than we requested. Should we propagate the real expiration date, or the desired expiration date?

        foreach (SecretStore secondaryStore in SecondaryStores)
        {
            // secondaries don't store revocation dates.
            await secondaryStore.WriteSecretAsync(newValue, currentState, null, whatIf);
        }
    }

    private async Task<SecretValue> OriginateNewValueAsync(string operationId, 
        DateTimeOffset invocationTime, 
        SecretState currentState,
        bool whatIf)
    {
        DateTimeOffset newExpirationDate = invocationTime.Add(RotationPeriod);

        SecretValue newValue = await OriginStore.OriginateValueAsync(currentState, newExpirationDate, whatIf);
        newValue.ExpirationDate ??= newExpirationDate;
        newValue.OperationId = operationId;
        return newValue;
    }

    private async Task RevokeRotationArtifactsAsync(bool whatIf)
    {
        DateTimeOffset invocationTime = this.timeProvider.GetCurrentDateTimeOffset();

        IEnumerable<SecretState> rotationArtifacts = await PrimaryStore.GetRotationArtifactsAsync();

        SecretStore[] storesSupportingRevocation = SecondaryStores
            .Append(OriginStore)
            .Append(PrimaryStore)
            .Where(store => store.CanRevoke)
            .Distinct() // Origin and Primary may be the same store
            .ToArray();

        foreach (SecretState rotationArtifact in rotationArtifacts.Where(
                     state => state.RevokeAfterDate < invocationTime))
        {
            this.logger.LogInformation(
                "Revoking secret operation id '{OperationId}' and revoke after date {RevokeAfterDate}",
                rotationArtifact.OperationId, rotationArtifact.RevokeAfterDate);

            foreach (SecretStore stateStore in storesSupportingRevocation)
            {
                Func<Task>? revocationAction = stateStore.GetRevocationActionAsync(rotationArtifact, whatIf);

                if (revocationAction != null)
                {
                    this.logger.LogInformation("Processing revocation action for store '{StateStore}'", stateStore.Name);
                    await revocationAction();
                }
            }
        }
    }
}
