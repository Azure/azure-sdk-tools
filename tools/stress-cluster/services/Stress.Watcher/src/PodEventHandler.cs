using System;
using System.Linq;
using System.Threading.Tasks;
using Stress.Watcher.Extensions;
using k8s;
using k8s.Models;

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

        public void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        public void HandlePodEvent(WatchEventType type, V1Pod pod)
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

        public async Task ResumeChaos(WatchEventType type, V1Pod pod)
        {
            if (!ShouldStartPodChaos(type, pod))
            {
                return;
            }

            await StartChaosResources(pod);
            Log($"Started chaos resources for pod {pod.NamespacedName()};");
            await Client.PatchNamespacedPodAsync(PodChaosHandledPatchBody, pod.Name(), pod.Namespace());
            Log($"Annotated pod chaos started for {pod.NamespacedName()};");
        }

        public async Task StartChaosResources(V1Pod pod)
        {
            var chaos = await ChaosClient.ListNamespacedAsync(pod.Namespace());
            var tasks = chaos.Items
                        .Where(cr => ShouldStartChaos(cr, pod))
                        .Select(async cr =>
                        {
                            await Client.PatchNamespacedCustomObjectWithHttpMessagesAsync(
                                    PodChaosResumePatchBody, ChaosClient.Group, ChaosClient.Version,
                                    pod.Namespace(), cr.Kind.ToLower(), cr.Metadata.Name);

                            Log($"Started {cr.Kind} {cr.Metadata.Name} for pod {pod.NamespacedName()}");
                        });

            await Task.WhenAll(tasks);
        }

        public bool ShouldStartChaos(GenericChaosResource chaos, V1Pod pod)
        {
            if (chaos.Spec.Selector.LabelSelectors.TestInstance != pod.TestInstance())
            {
                return false;
            }

            return chaos.IsPaused();
        }

        public bool ShouldStartPodChaos(WatchEventType type, V1Pod pod)
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

            if (String.IsNullOrEmpty(pod.TestInstance()))
            {
                Log($"Pod {pod.NamespacedName()} has chaos label but missing or empty {GenericChaosResource.TestInstanceLabelKey} label.");
                return false;
            }

            if (pod.HasChaosStarted())
            {
                Log($"Pod {pod.NamespacedName()} chaos has started.");
                return false;
            }

            return true;
        }
    }
}
