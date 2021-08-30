using System;
using System.Threading.Tasks;
using k8s.Models;
using k8s;

namespace chaos_watcher
{
    class PodEventHandler
    {
        private string podChaosHandledPatch = @"
{
    ""metadata"": {
        ""annotations"": {
            ""stress/chaos.started"": ""true""
        }
    }
}";

        private string podChaosResumePatch = @"
{
    ""metadata"": {
        ""annotations"": {
            ""experiment.chaos-mesh.org/pause"": null
        }
    }
}";

        private Kubernetes Client;
        private GenericClient GenericClient;

        public PodEventHandler(Kubernetes client, GenericClient genericClient)
        {
            Client = client;
            GenericClient = genericClient;
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
            var task = ResumeChaos(type, pod);
            task.ContinueWith(t => {
                if (t.Exception != null) {
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

            Log($"Marking pod chaos started for {pod.Namespace()}/{pod.Name()};");
            var body = new V1Patch(podChaosHandledPatch, V1Patch.PatchType.MergePatch);
            Client.PatchNamespacedPod(body, pod.Name(), pod.Namespace());

            await StartChaosResources(pod);
        }

        private async Task StartChaosResources(V1Pod pod)
        {
            Log($"Resuming chaos for {pod.Namespace()}/{pod.Name()};");

            var body = new V1Patch(podChaosResumePatch, V1Patch.PatchType.MergePatch);
            var resp = await Client.PatchNamespacedCustomObjectWithHttpMessagesAsync(
                        body,
                        "chaos-mesh.org",
                        "v1alpha1",
                        pod.Namespace(),
                        "networkchaos",
                        "network-example-15");

            Log("result");
            Log(resp.Response.StatusCode.ToString());
            Log("fin");
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

            if (!pod.Metadata.Labels.ContainsKey("testInstance"))
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
