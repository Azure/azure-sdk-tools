using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stress.Watcher.Extensions;
using k8s;
using k8s.Models;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.SystemConsole.Themes;
using System.Collections.Generic;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ManagedServiceIdentities;

namespace Stress.Watcher
{
    public class NamespaceEventHandler
    {
        private Kubernetes Client;
        private ArmClient ArmClient;
        private string SubscriptionId;
        private string ClusterGroup;
        private Serilog.Core.Logger Logger;
        private List<string> ExcludedNamespaces = new List<string> { "kube-system", "kube-public", "kube-node-lease", "gatekeeper-system", "stress-infra", "default" };
        private string WatchNamespace = "";

        // Concurrent Federated Identity Credentials writes under the same managed identity are not supported
        private static readonly SemaphoreSlim FederatedCredentialWriteSemaphore = new(1, 1);

        private Dictionary<string, UserAssignedIdentityResource> WorkloadAppCache = [];

        public List<string> WorkloadAppPool;
        public string WorkloadAppIssuer;

        public NamespaceEventHandler(
            Kubernetes client,
            ArmClient armClient,
            string subscriptionId,
            string clusterGroup,
            // GraphServiceClient graphClient,
            List<string> workloadAppPool,
            string workloadAppIssuer,
            string watchNamespace = ""
        )
        {
            Client = client;
            ArmClient = armClient;
            SubscriptionId = subscriptionId;
            ClusterGroup = clusterGroup;
            // GraphClient = graphClient;
            WorkloadAppPool = workloadAppPool;
            WorkloadAppIssuer = workloadAppIssuer;
            WatchNamespace = watchNamespace;

            Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich
                .FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:hh:mm:ss} {Level:u3}] {Message,-30:lj} {Properties:j}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code
                )
                .CreateLogger();
        }

        public async Task SyncCredentials()
        {
            try
            {
                Logger.Information($"Waiting for federated credential write semaphore");
                await FederatedCredentialWriteSemaphore.WaitAsync();
                await _syncCredentials();
            }
            finally
            {
                Logger.Information("Releasing federated credential write semaphore");
                FederatedCredentialWriteSemaphore.Release();
            }
        }

        public async Task _syncCredentials()
        {
            Logger.Information("Syncing namespaced federated credentials, this may take a minute...");

            var namespaces = await Client.ListNamespaceAsync();
            foreach (var app in WorkloadAppPool)
            {
                var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(SubscriptionId, ClusterGroup, app);
                var userAssignedIdentity = ArmClient.GetUserAssignedIdentityResource(resourceId);
                var identityResource = await userAssignedIdentity.GetAsync();
                var fedCreds = userAssignedIdentity.GetFederatedIdentityCredentials();
                await foreach (var item in fedCreds.GetAllAsync())
                {
                    if (!namespaces.Items.Any(ns => item.Data.Name == CreateFederatedIdentityCredentialName(ns)))
                    {
                        if (!string.IsNullOrEmpty(WatchNamespace) && item.Data.Name != CreateFederatedIdentityCredentialName(WatchNamespace))
                        {
                            Logger.Information($"Skipping delete federated credential '{item.Data.Name}' because it is not the watched namespace '{WatchNamespace}'");
                            continue;
                        }
                        // Only perform delete operations for namespace state that may have changed if the watcher was not running.
                        // Any create operations will be handled after initialization as the watch stream processes all active namespaces on startup
                        Logger.Information($"Deleting federated identity credential '{item.Data.Name}' for managed identity '{app}' as the corresponding namespace no longer exists.");
                        WorkloadAppCache.Remove(item.Data.Name);
                        var lro = await item.DeleteAsync(Azure.WaitUntil.Completed);
                    }
                    else
                    {
                        WorkloadAppCache[item.Data.Name] = identityResource.Value;
                    }
                }
            }

            Logger.Information($"Federated credential sync complete. Cached {WorkloadAppCache.Count} federated credentials.");
        }

        public async Task Watch(CancellationToken cancellationToken)
        {
            string resourceVersion = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Logger.Information("Starting namespace watch");
                    var listTask = Client.CoreV1.ListNamespaceWithHttpMessagesAsync(
                        allowWatchBookmarks: true,
                        watch: true,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken
                    );
                    var tcs = new TaskCompletionSource();
                    using var watcher = listTask.Watch<V1Namespace, V1NamespaceList>(
                        (type, ns) =>
                        {
                            resourceVersion = ns.ResourceVersion();
                            HandleNamespaceEvent(type, ns);
                        },
                        (err) =>
                        {
                            Logger.Error(err, "Handling error event for namespace watch stream.");
                            if (err is KubernetesException kubernetesError)
                            {
                                // Handle "too old resource version"
                                if (string.Equals(kubernetesError.Status.Reason, "Expired", StringComparison.Ordinal))
                                {
                                    resourceVersion = null;
                                }
                            }
                            tcs.TrySetException(err);
                            throw err;
                        },
                        () =>
                        {
                            Logger.Warning("Namespace watch has closed.");
                            tcs.TrySetResult();
                        }
                    );
                    using var registration = cancellationToken.Register(watcher.Dispose);
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with Namespace watch stream.");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void HandleNamespaceEvent(WatchEventType eventType, V1Namespace ns)
        {
            if (ExcludedNamespaces.Contains(ns.Name()) || string.IsNullOrEmpty(ns.Name()))
            {
                return;
            }
            if (!string.IsNullOrEmpty(WatchNamespace) && ns.Name() != WatchNamespace)
            {
                Logger.Information($"Skipping namespace '{ns.Name()}' because it is not the watched namespace '{WatchNamespace}'");
                return;
            }

            using (LogContext.PushProperty("namespace", ns.Name()))
            {
                if (eventType == WatchEventType.Added)
                {
                    InitializeWorkloadIdForNamespace(ns).ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            Logger.Error(t.Exception, "Error creating federated identity credential.");
                            return;
                        }
                    });
                }
                else if (eventType == WatchEventType.Deleted)
                {
                    DeleteFederatedIdentityCredential(ns).ContinueWith(t =>
                    {
                        Logger.Information("Releasing federated credential write semaphore");
                        FederatedCredentialWriteSemaphore.Release();
                        if (t.Exception != null)
                        {
                            Logger.Error(t.Exception, "Error deleting federated identity credential.");
                        }
                    });
                }
            }
        }

        public string CreateFederatedIdentityCredentialName(V1Namespace ns)
        {
            return CreateFederatedIdentityCredentialName(ns.Name());
        }

        public string CreateFederatedIdentityCredentialName(string ns)
        {
            return $"stress-{ns}";
        }

        public async Task InitializeWorkloadIdForNamespace(V1Namespace ns)
        {
            UserAssignedIdentityResource selectedWorkloadIdentity = null;
            try
            {
                selectedWorkloadIdentity = await CreateFederatedIdentityCredential(ns);
            }
            finally
            {
                Logger.Information("Releasing federated credential write semaphore");
                FederatedCredentialWriteSemaphore.Release();
            }

            var identityData = await selectedWorkloadIdentity.GetAsync();
            var selectedWorkloadAppId = identityData.Value.Data.ClientId.ToString();

            var meta = new V1ObjectMeta()
            {
                Name = ns.Name(),
                NamespaceProperty = ns.Name(),
                Annotations = new Dictionary<string, string>(){
                    { "azure.workload.identity/client-id", selectedWorkloadAppId }
                }
            };
            var serviceAccount = new V1ServiceAccount(metadata: meta);
            var allAccounts = await Client.ListNamespacedServiceAccountAsync(ns.Name());
            if (allAccounts.Items.Any(sa => sa.Name() == ns.Name()))
            {
                Logger.Information($"Service account '{ns.Name()}/{ns.Name()}' already exists, skipping creation.");
                return;
            }
            await Client.CreateNamespacedServiceAccountAsync(serviceAccount, ns.Name());
            Logger.Information($"Created service account '{ns.Name()}/{ns.Name()}' with workload client id '{selectedWorkloadAppId}'");
        }

        public async Task<UserAssignedIdentityResource> CreateFederatedIdentityCredential(V1Namespace ns)
        {
            var credentialName = CreateFederatedIdentityCredentialName(ns);
            var subject = $"system:serviceaccount:{ns.Name()}:{ns.Name()}";
            string selectedWorkloadApp = "";
            UserAssignedIdentityResource selectedIdentity = null;

            // Wait on the list call so we don't have an outdated collection state for multiple namespaces events processed together
            // This is a slow sequence of calls to lock on (several seconds) but frequency is only high enough for this to matter
            // on service startup with a large number of namespaces that haven't been initialized.
            Logger.Information($"Waiting for federated credential write semaphore");
            await FederatedCredentialWriteSemaphore.WaitAsync();

            if (WorkloadAppCache.ContainsKey(credentialName))
            {
                Logger.Information($"Found cache entry for federated credential {credentialName}, returning identity {WorkloadAppCache[credentialName].Data.ClientId}");
                return await WorkloadAppCache[credentialName].GetAsync();
            }

            foreach (var workloadApp in WorkloadAppPool)
            {
                var userAssignedIdentityResourceId = UserAssignedIdentityResource.CreateResourceIdentifier(SubscriptionId, ClusterGroup, workloadApp);
                var userAssignedIdentity = ArmClient.GetUserAssignedIdentityResource(userAssignedIdentityResourceId);
                Logger.Information($"Getting federated identity credentials for managed identity '{workloadApp}'");
                var fedCreds = userAssignedIdentity.GetFederatedIdentityCredentials();

                // Federated credentials maxes out per managed identity at 20, leave some wiggle room due to list state delays
                Logger.Information($"Found {fedCreds.Count()} creds for {workloadApp}");
                if (fedCreds.Count() < 19)
                {
                    selectedWorkloadApp = workloadApp;
                    selectedIdentity = userAssignedIdentity;
                    break;
                }
            }

            if (string.IsNullOrEmpty(selectedWorkloadApp) || selectedIdentity == null)
            {
                var errorMessage = "No available managed identities to create federated identity credential. Add more to the pool.";
                Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }

            var resourceGroupResourceId = ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, ClusterGroup);
            var resourceGroupResource = ArmClient.GetResourceGroupResource(resourceGroupResourceId);
            var collection = resourceGroupResource.GetUserAssignedIdentities();

            var federatedIdentityCredentialResourceId = FederatedIdentityCredentialResource.CreateResourceIdentifier(
                    SubscriptionId, ClusterGroup, selectedWorkloadApp, credentialName);
            var federatedIdentityCredential = ArmClient.GetFederatedIdentityCredentialResource(federatedIdentityCredentialResourceId);

            var fedCredData = new FederatedIdentityCredentialData()
            {
                IssuerUri = new Uri(WorkloadAppIssuer),
                // Azure AKS workload identity enabled service accounts follow this scheme for subject:
                // system:serviceaccount:{namespace}:{service account name}
                Subject = subject,
                Audiences = {
                    "api://AzureADTokenExchange",
                },
            };

            Logger.Information($"Creating/updating federated identity credential '{credentialName}' " +
                               $"with subject '{subject}' for managed identity '{selectedWorkloadApp}'");
            var lro = await federatedIdentityCredential.UpdateAsync(Azure.WaitUntil.Completed, fedCredData);
            WorkloadAppCache[credentialName] = selectedIdentity;
            Logger.Information($"Created federated identity credential '{lro.Value.Data.Name}'");

            return selectedIdentity;
        }

        public async Task DeleteFederatedIdentityCredential(V1Namespace ns)
        {
            Logger.Information($"Waiting for federated credential write semaphore");
            await FederatedCredentialWriteSemaphore.WaitAsync();

            var credentialName = CreateFederatedIdentityCredentialName(ns);
            var workloadApp = "";

            WorkloadAppCache.Remove(credentialName);

            foreach (var app in WorkloadAppPool)
            {
                var resourceId = UserAssignedIdentityResource.CreateResourceIdentifier(SubscriptionId, ClusterGroup, app);
                var userAssignedIdentity = ArmClient.GetUserAssignedIdentityResource(resourceId);
                var fedCreds = userAssignedIdentity.GetFederatedIdentityCredentials();
                await foreach (var item in fedCreds.GetAllAsync())
                {
                    if (item.Data.Name == credentialName)
                    {
                        workloadApp = app;
                        break;
                    }
                }
                if (!String.IsNullOrEmpty(workloadApp))
                {
                    break;
                }
            }

            if (string.IsNullOrEmpty(workloadApp))
            {
                Logger.Warning($"Federated identity credential '{credentialName}' not found in workload app pool. Skipping delete.");
                return;
            }

            var federatedIdentityCredentialResourceId = FederatedIdentityCredentialResource.CreateResourceIdentifier(
                    SubscriptionId, ClusterGroup, workloadApp, credentialName);
            var federatedIdentityCredential = ArmClient.GetFederatedIdentityCredentialResource(federatedIdentityCredentialResourceId);

            Logger.Information($"Deleting federated identity credential '{credentialName}' for managed identity '{workloadApp}'");
            var lro = await federatedIdentityCredential.DeleteAsync(Azure.WaitUntil.Completed);
            Logger.Information($"Deleted federated identity credential '{credentialName}'");
        }
    }
}
