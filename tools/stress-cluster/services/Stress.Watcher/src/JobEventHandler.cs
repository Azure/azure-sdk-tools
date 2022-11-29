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
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Stress.Watcher
{
    public class JobEventHandler
    {
        private Kubernetes Client;
        private GenericChaosClient ChaosClient;

        private ArmClient ARMClient;

        private Serilog.Core.Logger Logger;

        public string Namespace;

        public JobEventHandler(
            Kubernetes client,
            GenericChaosClient chaosClient,
            ArmClient armClient,
            string watchNamespace = ""
        )
        {
            Client = client;
            ChaosClient = chaosClient;
            ARMClient = armClient;
            Namespace = watchNamespace;

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

        public async Task Watch(CancellationToken cancellationToken)
        {
            string resourceVersion = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Logger.Information("Starting job watch");
                    var listTask = Client.BatchV1.ListJobForAllNamespacesWithHttpMessagesAsync(
                        allowWatchBookmarks: true,
                        watch: true,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken
                    );
                    var tcs = new TaskCompletionSource();
                    using var watcher = listTask.Watch<V1Job, V1JobList>(
                        (type, job) => {
                            resourceVersion = job.ResourceVersion();
                            HandleJobEvent(type, job);
                        },
                        (err) =>
                        {
                            Logger.Error(err, "Handling error event for job watch stream.");
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
                        () => {
                            Logger.Warning("Job watch has closed.");
                            tcs.TrySetResult();
                        }
                    );
                    using var registration = cancellationToken.Register(watcher.Dispose);
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with job watch stream.");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void HandleJobEvent(WatchEventType eventType, V1Job job)
        {
            using (LogContext.PushProperty("namespace", job.Namespace()))
            using (LogContext.PushProperty("job", job.Name()))
            {
                DeleteResources(job, eventType).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception, "Error deleting resources.");
                    }
                });
            }
        }

        public async Task DeleteResources(V1Job job, WatchEventType eventType)
        {
            if (!ShouldDeleteResources(job, eventType))
            {
                return;
            }

            var rgName = GetResourceGroupName(job);

            if (string.IsNullOrEmpty(rgName))
            {
                return;
            }

            Subscription subscription = ARMClient.DefaultSubscription;

            ResourceGroup resourceGroup;
            try {
                resourceGroup = await subscription.GetResourceGroups().GetAsync(rgName);
            } catch (Exception) {
                Logger.Error($"Failed to get resource group '{rgName}' using subsription id '{subscription.Id}'");
                return;
            }

            Logger.Information($"Deleting resources for group {rgName}");
            await resourceGroup.DeleteAsync();
            Logger.Information($"Deleted resources for group {rgName}");
        }

        public bool ShouldDeleteResources(V1Job job, WatchEventType eventType)
        {
            if (!string.IsNullOrEmpty(Namespace) && Namespace != job.Namespace())
            {
                Logger.Debug($"Namespace filter mismatch {Namespace} != {job.Namespace()}, skipping resource deletion.");
                return false;
            }

            var initContainers = job.Spec?.Template?.Spec?.InitContainers;
            if (initContainers == null || initContainers.Count() == 0) {
                Logger.Debug($"No deploy init container found, skipping resource deletion.");
                return false;
            }

            var deployContainers = initContainers.Where(c => c.Name == "init-azure-deployer");
            if (deployContainers.Count() == 0)
            {
                Logger.Debug($"No deploy init container found, skipping resource deletion.");
                return false;
            }

            // Handle helm uninstall, helm upgrade, namespace deletion, job deletion, etc.
            if (eventType == WatchEventType.Deleted)
            {
                return true;
            }

            /*
               Check if job is done by determing whether any conditions are satifisfied as "True"
               This is most flexible because checking succeeded/failed counts will not be flexible
               when there are multiple pods specified for the job.

               Example succeeded state
               --------------------
                 status:
                   completionTime: "2022-10-26T20:53:20Z"
                   conditions:
                   - lastProbeTime: "2022-10-26T20:53:20Z"
                     lastTransitionTime: "2022-10-26T20:53:20Z"
                     status: "True"
                     type: Complete
                   ready: 0
                   startTime: "2022-10-26T20:52:28Z"
                   succeeded: 1
               --------------------

               Example failed state
               --------------------
                 status:
                    conditions:
                    - lastProbeTime: "2022-10-26T21:05:39Z"
                      lastTransitionTime: "2022-10-26T21:05:39Z"
                      message: Job has reached the specified backoff limit
                      reason: BackoffLimitExceeded
                      status: "True"
                      type: Failed
                    failed: 1
                    ready: 0
                    startTime: "2022-10-26T21:04:58Z"
               --------------------
           */
            var isCompleted = job.Status?.Conditions?.Any(c => c.Status == "True");
            if (isCompleted != true)
            {
                Logger.Debug($"Job is not completed, skipping resource deletion.");
                return false;
            }

            // NOTE: this will not handle labels added to a pod after deployment
            job.Metadata.Labels.TryGetValue("Skip.RemoveTestResources", out var skipJobRemove);
            job.Spec.Template.Metadata.Labels.TryGetValue("Skip.RemoveTestResources", out var skipPodRemove);

            if (skipJobRemove == "true" || skipPodRemove == "true")
            {
                Logger.Information($"Resource has Skip.RemoveTestResources=true label, skipping resource deletion.");
                return false;
            }

            return true;
        }

        public string GetResourceGroupName(V1Job job)
        {
            var deployContainers = job.Spec?.Template?.Spec?.InitContainers.Where(c => c.Name == "init-azure-deployer");;
            var envVars = deployContainers?.First().Env;
            if (envVars == null) {
                return "";
            }

            var rgName = envVars.Where(e => e.Name == "RESOURCE_GROUP_NAME").Select(e => e.Value);
            if (rgName.Count() == 0)
            {
                Logger.Error("Cannot find the env variable 'RESOURCE_GROUP_NAME' on the init container 'init-azure-deployer' spec.");
                return "";
            }
            if (rgName.First() == null) {
                Logger.Error("Env variable RESOURCE_GROUP_NAME does not have a value.");
                return "";
            }

            return rgName.First().ToString();
        }
    }
}
