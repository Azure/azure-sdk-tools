using System;
using System.Threading;
using k8s;
using k8s.Models;

namespace chaos_watcher
{
    class ChaosWatcher
    {
        static void Main(string[] args)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            var client = new Kubernetes(config);

            var podListResp = client.ListPodForAllNamespacesWithHttpMessagesAsync(watch: true);
            using (podListResp.Watch<V1Pod, V1PodList>(HandlePodEvent))
            {
                var ctrlc = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
                ctrlc.Wait();
            };
        }

        private static void HandlePodEvent(WatchEventType type, V1Pod pod)
        {
            Console.WriteLine("--- on watch event ---");
            var value = "";
            if (pod.Metadata.Labels.TryGetValue("chaos", out value) && value == "true") {
                Console.WriteLine("Found chaos pod");
            }
            Console.WriteLine(type);
            Console.WriteLine(pod.Metadata.Name);
            Console.WriteLine("--- end watch event ---");
        }
    }
}
