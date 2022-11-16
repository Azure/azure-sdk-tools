using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using CommandLine;
using Azure.Identity;
using Azure.ResourceManager;
using dotenv.net;

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

            DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] {"/mnt/outputs/.env"}));
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            // Default to 'Azure SDK Developer Playground' subscription when testing locally outside of the stress cluster.
            subscriptionId = subscriptionId ?? "faa080af-c1d8-40ad-9cce-e1a450ca5b57";

            ArmClient armClient = new ArmClient(subscriptionId, new DefaultAzureCredential());

            var podEventHandler = new PodEventHandler(client, chaosClient, armClient, options.Namespace);
            var jobEventHandler = new JobEventHandler(client, chaosClient, armClient, options.Namespace);

            var cts = new CancellationTokenSource();
            var taskList = new List<Task>
            {
                Task.Run(async () => { await podEventHandler.Watch(cts.Token); }),
                Task.Run(async () => { await jobEventHandler.Watch(cts.Token); }),
            };

            await Task.WhenAll(taskList.ToArray());
        }
    }
}
