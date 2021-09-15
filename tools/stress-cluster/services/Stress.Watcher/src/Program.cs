using System;
using System.Threading;
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

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    Program(o);
                });
        }

        static void Program(Options options)
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
            using Watcher<V1Pod> watcher = podEventHandler.Watch();

            var ctrlc = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
            ctrlc.Wait();
        }
    }
}
