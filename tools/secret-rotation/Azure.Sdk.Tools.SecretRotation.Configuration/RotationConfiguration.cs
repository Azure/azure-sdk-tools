using System.Collections.ObjectModel;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Configuration;

public class RotationConfiguration
{
    private readonly IDictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories;

    private RotationConfiguration(
        IDictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories, 
        IEnumerable<PlanConfiguration> planConfigurations)
    {
        this.storeFactories = storeFactories;
        PlanConfigurations = new ReadOnlyCollection<PlanConfiguration>(planConfigurations.ToArray());
    }

    public ReadOnlyCollection<PlanConfiguration> PlanConfigurations { get; }

    public static RotationConfiguration From(IList<string> names,
        IList<string> tags,
        string directoryPath,
        IDictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories)
    {
        IEnumerable<string> jsonFiles;

        try
        {
            jsonFiles = Directory
                .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            throw new RotationConfigurationException(
                $"Error loading files in directory '{directoryPath}'.",
                ex);
        }

        IEnumerable<PlanConfiguration> planConfigurations = jsonFiles
            .Select(file => PlanConfiguration.TryLoadFromFile(file, out var plan) ? plan : null)
            .Where(x => x != null)
            .Select(x => x!);

        if (names.Any())
        {
            planConfigurations = planConfigurations
                .Where(plan => names.Contains(plan.Name, StringComparer.OrdinalIgnoreCase));
        }

        if (tags.Any())
        {
            planConfigurations = planConfigurations
                .Where(plan => tags.All(tag => plan.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        planConfigurations = planConfigurations.ToArray();

        if (!planConfigurations.Any())
        {
            throw new RotationConfigurationException("Unable to locate any configuration files matching the provided names and tags");
        }

        var configuration = new RotationConfiguration(storeFactories, planConfigurations);

        return configuration;
    }

    public IEnumerable<RotationPlan> GetAllRotationPlans(ILogger logger, TimeProvider timeProvider)
    {
        return PlanConfigurations.Select(planConfiguration =>
            ResolveRotationPlan(planConfiguration, logger, timeProvider));
    }

    private RotationPlan ResolveRotationPlan(PlanConfiguration planConfiguration, ILogger logger,
        TimeProvider timeProvider)
    {
        string? name = planConfiguration.Name;

        if (string.IsNullOrEmpty(name))
        {
            throw new RotationConfigurationException("Error processing plan configuration. Name is null or empty.");
        }

        var validationErrors = new List<string>();
        SecretStore? origin = GetOriginStore(planConfiguration, validationErrors);
        SecretStore? primary = GetPrimaryStore(planConfiguration, origin, validationErrors);
        IList<SecretStore> secondaries = GetSecondaryStores(planConfiguration, validationErrors);

        string errorPrefix = $"Error processing plan configuration '{name}'.";

        if (origin == null)
        {
            validationErrors.Add($"{errorPrefix} Unable to resolve origin store.");
        }

        if (primary == null)
        {
            validationErrors.Add($"{errorPrefix} Unable to resolve primary store.");
        }

        if (planConfiguration.RotationPeriod == null)
        {
            validationErrors.Add($"{errorPrefix} Property 'rotationPeriod' cannot be null");
        }

        if (planConfiguration.RotationThreshold == null)
        {
            validationErrors.Add($"{errorPrefix} Property 'rotationThreshold' cannot be null");
        }

        if (validationErrors.Any())
        {
            throw new RotationConfigurationException(string.Join('\n', validationErrors));
        }

        var plan = new RotationPlan(
            logger,
            timeProvider,
            name,
            origin!,
            primary!,
            secondaries,
            planConfiguration.RotationThreshold!.Value,
            planConfiguration.RotationPeriod!.Value,
            planConfiguration.RevokeAfterPeriod);

        return plan;
    }

    private IList<SecretStore> GetSecondaryStores(PlanConfiguration planConfiguration, List<string> validationErrors)
    {
        var secondaryStores = new List<SecretStore>();

        (StoreConfiguration Configuration, int Index)[] secondaryStoreConfigurations = planConfiguration
            .StoreConfigurations
            .Select((configuration, index) => (Configuration: configuration, Index: index))
            .Where(x => x.Configuration is { IsPrimary: false, IsOrigin: false })
            .ToArray();

        foreach ((StoreConfiguration storeConfiguration, int index) in secondaryStoreConfigurations)
        {
            string configurationKey = $"{planConfiguration.Name}.stores[{index}]";
            SecretStore store = ResolveStore(configurationKey, storeConfiguration);

            if (!store.CanWrite)
            {
                AddCapabilityError(validationErrors, store, nameof(store.CanWrite), "Secondary");
            }

            secondaryStores.Add(store);
        }

        return secondaryStores;
    }

    private SecretStore? GetOriginStore(PlanConfiguration planConfiguration, List<string> validationErrors)
    {
        (StoreConfiguration Configuration, int Index)[] originStoreConfigurations = planConfiguration
            .StoreConfigurations
            .Select((configuration, index) => (Configuration: configuration, Index: index))
            .Where(x => x.Configuration.IsOrigin)
            .ToArray();

        if (originStoreConfigurations.Length != 1)
        {
            validationErrors.Add($"Error processing plan configuration '{planConfiguration.Name}'. " +
                                 $"Exactly 1 store should be marked IsOrigin");
            return null;
        }

        (StoreConfiguration Configuration, int Index) storeConfigurationAndIndex = originStoreConfigurations[0];
        string configurationKey = $"{planConfiguration.Name}.stores[{storeConfigurationAndIndex.Index}]";

        SecretStore store = ResolveStore(configurationKey, storeConfigurationAndIndex.Configuration);

        if (!store.CanOriginate)
        {
            AddCapabilityError(validationErrors, store, nameof(store.CanOriginate), "Origin");
        }

        return store;
    }

    private SecretStore? GetPrimaryStore(PlanConfiguration planConfiguration, SecretStore? originStore,
        List<string> validationErrors)
    {
        (StoreConfiguration Configuration, int Index)[] primaryStoreConfigurations = planConfiguration
            .StoreConfigurations
            .Select((configuration, index) => (Configuration: configuration, Index: index))
            .Where(x => x.Configuration.IsPrimary)
            .ToArray();

        if (primaryStoreConfigurations.Length != 1)
        {
            validationErrors.Add($"Error processing plan configuration '{planConfiguration.Name}'. " +
                                 $"Exactly 1 store should be marked IsPrimary");
            return null;
        }

        StoreConfiguration storeConfiguration = primaryStoreConfigurations[0].Configuration;
        int index = primaryStoreConfigurations[0].Index;
        string configurationKey = $"{planConfiguration.Name}.stores[{index}]";

        SecretStore store = storeConfiguration.IsOrigin && originStore != null
            ? originStore
            : ResolveStore(configurationKey, storeConfiguration);

        if (!store.CanRead)
        {
            AddCapabilityError(validationErrors, store, nameof(store.CanRead), "Primary");
        }

        bool mustAnnotateCompletion = storeConfiguration.IsOrigin ||
            planConfiguration.StoreConfigurations.Any(x => x.UpdateAfterPrimary);

        if (mustAnnotateCompletion && !store.CanAnnotate)
        {
            AddCapabilityError(validationErrors, store, nameof(store.CanAnnotate), "Primary");
        }

        if (!storeConfiguration.IsOrigin && !store.CanWrite)
        {
            // A non origin primary has to support Write because it doesn't originate values
            AddCapabilityError(validationErrors, store, nameof(store.CanWrite), "Primary");
        }

        return store;
    }

    private void AddCapabilityError(List<string> validationErrors, SecretStore store, string capabilityName, 
        string storeUsage)
    {
        string typeName = store.GetType().Name;
        string errorMessage = $"Error processing store configuration for store named '{store.Name}'. " +
                              $"Store type '{typeName}' cannot be used as {storeUsage}. " +
                              $"{capabilityName} returned false";
        validationErrors.Add(errorMessage);
    }

    private SecretStore ResolveStore(string configurationKey, StoreConfiguration storeConfiguration)
    {
        string storeName = storeConfiguration.ResolveStoreName(configurationKey);

        if (storeConfiguration.Type == null)
        {
            throw new RotationConfigurationException(
                $"Error processing store configuration for store named '{storeName}'. Type cannot be null.");
        }

        if (!this.storeFactories.TryGetValue(storeConfiguration.Type,
                out Func<StoreConfiguration, SecretStore>? factory))
        {
            throw new RotationConfigurationException($"Error processing store configuration for store named '{storeName}'. " +
                                                     $"Store type '{storeConfiguration.Type}' not registered");
        }

        try
        {
            SecretStore store = factory.Invoke(storeConfiguration);

            store.Name = storeName;
            store.UpdateAfterPrimary = storeConfiguration.UpdateAfterPrimary;
            store.IsOrigin = storeConfiguration.IsOrigin;
            store.IsPrimary = storeConfiguration.IsPrimary;

            return store;
        }
        catch (Exception ex)
        {
            throw new RotationConfigurationException($"Error processing store configuration for store named '{storeName}'.", ex);
        }
    }
}
