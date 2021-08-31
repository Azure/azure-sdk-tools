using System;
using System.Linq;
using System.Threading.Tasks;
using k8s.Models;
using k8s;

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

        public PodEventHandler(Kubernetes client, GenericChaosClient chaosClient)
        {
            Client = client;
            ChaosClient = chaosClient;

            PodChaosHandledPatchBody = new V1Patch(PodChaosHandledPatch, V1Patch.PatchType.MergePatch);
            PodChaosResumePatchBody = new V1Patch(PodChaosResumePatch, V1Patch.PatchType.MergePatch);
        }

        public Watcher<V1Pod> Watch()
        {
            return Client
              .ListPodForAllNamespacesWithHttpMessagesAsync(watch: true)
              .Watch<V1Pod, V1PodList>(HandlePodEvent);
        }

        private void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        private void HandlePodEvent(WatchEventType type, V1Pod pod)
        {
            ResumeChaos(type, pod).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    // TODO: handle watch event re-queue on failure
                    Log(t.Exception.ToString());
                }
            });
        }

        private async Task ResumeChaos(WatchEventType type, V1Pod pod)
        {
            if (!ShouldStartPodChaos(type, pod))
            {
                return;
            }

            await StartChaosResources(pod);
            Log($"Started chaos resources for pod {pod.Namespace()}/{pod.Name()};");
            await Client.PatchNamespacedPodAsync(PodChaosHandledPatchBody, pod.Name(), pod.Namespace());
            Log($"Annotated pod chaos started for {pod.Namespace()}/{pod.Name()};");
        }

        private async Task StartChaosResources(V1Pod pod)
        {
            var chaos = await ChaosClient.ListNamespacedAsync(pod.Namespace());
            var tasks = chaos.Items
                        .Where(cr => ShouldStartChaos(cr, pod))
                        .Select(async cr => {
                            await Client.PatchNamespacedCustomObjectWithHttpMessagesAsync(
                                    PodChaosResumePatchBody, ChaosClient.Group, ChaosClient.Version,
                                    pod.Namespace(), cr.Kind.ToLower(), cr.Metadata.Name);

                            Log($"Started {cr.Kind} {cr.Metadata.Name} for pod {pod.Namespace()}/{pod.Name()}");
                        });

            await Task.WhenAll(tasks);
        }

        private bool ShouldStartChaos(GenericChaosResource chaos, V1Pod pod)
        {
            if (chaos.Spec.Selector.LabelSelectors.TestInstance != pod.Labels()["testInstance"])
            {
                return false;
            }

            var paused = "";
            chaos.Metadata.Annotations.TryGetValue("experiment.chaos-mesh.org/pause", out paused);
            if (paused != "true")
            {
                return false;
            }

            return true;
        }

        private bool ShouldStartPodChaos(WatchEventType type, V1Pod pod)
        {
            if ((type != WatchEventType.Added && type != WatchEventType.Modified) ||
                pod.Status.Phase != "Running")
            {
                return false;
            }

            if (!pod.Metadata.Labels.ContainsKey("chaos") || pod.Metadata.Labels["chaos"] != "true")
            {
                return false;
            }

            if (!pod.Labels().ContainsKey("testInstance"))
            {
                Log($"Pod {pod.Namespace()}/{pod.Name()} has chaos label but no test-instance label.");
                return false;
            }

            var started = "";
            if (pod.Metadata.Annotations != null &&
                pod.Metadata.Annotations.TryGetValue("stress/chaos.started", out started) &&
                started == "true")
            {
                Log($"Pod {pod.Namespace()}/{pod.Name()} chaos has started.");
                return false;
            }

            return true;
        }
    }
}
