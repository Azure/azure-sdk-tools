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

namespace Stress.Watcher
{
    public class PodEventHandler
    {
        private string PodChaosHandledPatch = @"
{
    ""metadata"": {
        ""annotations"": {
            ""stress/chaos.started"": ""true""
        }
    }
}";

        private string PodChaosResumePatch = @"
{
    ""metadata"": {
        ""annotations"": {
            ""experiment.chaos-mesh.org/pause"": null
        }
    }
}";

        private V1Patch PodChaosHandledPatchBody;
        private V1Patch PodChaosResumePatchBody;

        private Kubernetes Client;
        private GenericChaosClient ChaosClient;

        private ArmClient ARMClient;

        private Serilog.Core.Logger Logger;

        public string Namespace;

        public PodEventHandler(
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

            PodChaosHandledPatchBody = new V1Patch(PodChaosHandledPatch, V1Patch.PatchType.MergePatch);
            PodChaosResumePatchBody = new V1Patch(PodChaosResumePatch, V1Patch.PatchType.MergePatch);
        }

        public async Task Watch(CancellationToken cancellationToken)
        {
            string resourceVersion = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Logger.Information("Starting pod watch");

                    var listTask = Client.CoreV1.ListPodForAllNamespacesWithHttpMessagesAsync(
                        allowWatchBookmarks: true,
                        watch: true,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken
                    );
                    var tcs = new TaskCompletionSource();
                    using var watcher = listTask.Watch<V1Pod, V1PodList>(
                        (type, pod) => {
                            resourceVersion = pod.ResourceVersion();
                            HandlePodEvent(type, pod);
                        },
                        (err) =>
                        {
                            Logger.Error(err, "Handling error event for pod watch stream.");
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
                            Logger.Warning("Pod watch has closed.");
                            tcs.TrySetResult();
                        }
                    );
                    using var registration = cancellationToken.Register(watcher.Dispose);
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with pod watch stream.");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void HandlePodEvent(WatchEventType type, V1Pod pod)
        {
            using (LogContext.PushProperty("namespace", pod.Namespace()))
            using (LogContext.PushProperty("pod", pod.Name()))
            {
                ResumeChaos(type, pod).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        // TODO: handle watch event re-queue on failure
                        Logger.Error(t.Exception, "Error handling pod event.");
                    }
                });
            }
        }

        public async Task ResumeChaos(WatchEventType type, V1Pod pod)
        {
            if (!ShouldStartPodChaos(type, pod))
            {
                Logger.Debug("Skipping pod.");
                return;
            }

            await StartChaosResources(pod);
            Logger.Information($"Started chaos resources for pod");
            await Client.PatchNamespacedPodAsync(PodChaosHandledPatchBody, pod.Name(), pod.Namespace());
            Logger.Information($"Annotated pod chaos started");
        }

        public async Task StartChaosResources(V1Pod pod)
        {
            var chaos = await ChaosClient.ListNamespacedAsync(pod.Namespace());
            var tasks = chaos.Items
                        .Where(cr => ShouldStartChaos(cr, pod))
                        .Select(async cr =>
                        {
                            string plural = "";
                            foreach (string pluralName in Enum.GetNames(typeof(GenericChaosClient.ChaosResourcePlurals))) {
                                if (pluralName.Contains(cr.Kind.ToLower())) {
                                    plural = pluralName;
                                    break;
                                }
                            }

                            await Client.PatchNamespacedCustomObjectAsync(
                                    PodChaosResumePatchBody, ChaosClient.Group, ChaosClient.Version,
                                    pod.Namespace(), plural, cr.Metadata.Name);
                            using (LogContext.PushProperty("chaosResource", $"{cr.Kind}/{cr.Metadata.Name}"))
                            {
                                Logger.Information($"Started chaos for pod.");
                            }
                        });

            await Task.WhenAll(tasks);
        }

        public bool ShouldStartChaos(GenericChaosResource chaos, V1Pod pod)
        {
            if (chaos.Spec.GetTestInstance() != pod.TestInstance())
            {
                return false;
            }
            return chaos.IsPaused();
        }

        public bool ShouldStartPodChaos(WatchEventType type, V1Pod pod)
        {
            if (!string.IsNullOrEmpty(Namespace) && Namespace != pod.Namespace())
            {
                return false;
            }

            if ((type != WatchEventType.Added && type != WatchEventType.Modified) ||
                pod.Status.Phase != "Running")
            {
                return false;
            }

            var autoStart = "";
            pod.Metadata.Annotations?.TryGetValue("stress/chaos.autoStart", out autoStart);
            if (autoStart == "false")
            {
                return false;
            }

            if (pod.Metadata.Labels?.TryGetValue("chaos", out var chaos) != true || chaos != "true")
            {
                return false;
            }

            if (String.IsNullOrEmpty(pod.TestInstance()))
            {
                Logger.Information($"Pod has 'chaos' label but missing or empty {GenericChaosResource.TestInstanceLabelKey} label.");
                return false;
            }

            var started = "";
            pod.Metadata.Annotations?.TryGetValue("stress/chaos.started", out started);
            if (started == "true")
            {
                Logger.Information($"Pod is in chaos.started state.");
                return false;
            }

            return true;
        }
    }
}
