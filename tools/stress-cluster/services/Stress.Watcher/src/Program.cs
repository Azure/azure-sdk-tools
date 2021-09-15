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
                    Main(o);
                });
        }

        static void Main(Options options)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
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
