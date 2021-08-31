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
            var chaosClient = new GenericChaosClient(config);

            var podEventHandler = new PodEventHandler(client, chaosClient);
            using Watcher<V1Pod> watcher = podEventHandler.Watch();

            var ctrlc = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
            ctrlc.Wait();
        }
    }
}
