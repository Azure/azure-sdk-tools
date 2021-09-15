using System;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using CommandLine;

namespace Stress.Watcher
{
    class ChaosWatcher
    {
        public class Options
        {
            [Option('n', "namespace", Required = false, HelpText = "Watch specified namespace only.")]
            public string Namespace { get; set; }
        }

        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync<Options>(async o =>
                {
                    await Program(o);
                });
        }

        static async Task Program(Options options)
        {
            KubernetesClientConfiguration config;

            // Try to load kubeconfig file, if running locally,
            // otherwise try in cluster config (running in k8s container)
            try
            {
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            }
            catch (Exception)
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }

            var client = new Kubernetes(config);
            var chaosClient = new GenericChaosClient(config);

            var podEventHandler = new PodEventHandler(client, chaosClient, options.Namespace);
            
            var cts = new CancellationTokenSource();
            await podEventHandler.Watch(cts.Token);
        }
    }
}
