using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using CommandLine;
using Azure.Identity;
using Azure.ResourceManager;
using dotenv.net;
using YamlDotNet.RepresentationModel;

namespace Stress.Watcher
{
    class ChaosWatcher
    {
        public class Options
        {
            [Option('n', "namespace", Required = false, HelpText = "Watch specified namespace only.")]
            public string Namespace { get; set; }

            [Option('e', "environment", Required = false, HelpText = "Stress environment, specify for local testing")]
            public string Environment { get; set; }

            [Option('l', "local-addons-path", Required = false, HelpText = "Local stress-test-addons chart path, specify for local testing to load stress cluster config")]
            public string LocalAddonsPath { get; set; }

            [Option('i', "workload-app-issuer", Required = false, HelpText = "Cluster issuer URL for workload app token requests")]
            public string WorkloadAppIssuer { get; set; }

            [Option('w', "workload-app-pool", Required = false, HelpText = "Pool of workload identity apps to use for creating namespaced federated credentials")]
            public string WorkloadAppPool { get; set; }
        }

        class WorkloadAuthConfig
        {
            public List<string> WorkloadAppPool;
            public string WorkloadAppIssuer;
            public string SubscriptionId;
            public string ClusterGroup;
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
            KubernetesClientConfiguration k8sConfig;
            var isLocal = false;

            // Try to load kubeconfig file, if running locally,
            // otherwise try in cluster config (running in k8s container)
            try
            {
                k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                isLocal = true;
            }
            catch (Exception)
            {
                k8sConfig = KubernetesClientConfiguration.InClusterConfig();
            }

            var workloadConfig = GetWorkloadConfigValues(options, isLocal);

            var credential = new DefaultAzureCredential();
            var client = new Kubernetes(k8sConfig);
            var chaosClient = new GenericChaosClient(k8sConfig);
            var armClient = new ArmClient(credential, workloadConfig.SubscriptionId);
            var subscription = armClient.GetDefaultSubscription();

            var podEventHandler = new PodEventHandler(client, chaosClient, armClient, options.Namespace);
            var jobEventHandler = new JobEventHandler(client, chaosClient, armClient, subscription, options.Namespace);
            var namespaceEventHandler = new NamespaceEventHandler(
                client, armClient, workloadConfig.SubscriptionId, workloadConfig.ClusterGroup,
                workloadConfig.WorkloadAppPool, workloadConfig.WorkloadAppIssuer, options.Namespace);
            await namespaceEventHandler.SyncCredentials();
            _ = PollAndSyncCredentials(namespaceEventHandler, 288);  // poll every 12 hours

            var cts = new CancellationTokenSource();
            var taskList = new List<Task>
            {
                Task.Run(async () => { await podEventHandler.Watch(cts.Token); }),
                Task.Run(async () => { await jobEventHandler.Watch(cts.Token); }),
                Task.Run(async () => { await namespaceEventHandler.Watch(cts.Token); }),
            };

            await Task.WhenAll(taskList.ToArray());
        }

        static WorkloadAuthConfig GetWorkloadConfigValues(Options options, Boolean isLocal)
        {
            if (!isLocal)
            {
                DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { "/mnt/outputs/.env" }));
            }

            var workloadAppPool = options.WorkloadAppPool != null ? new List<string>(options.WorkloadAppPool.Split(',')) : null;
            var workloadAppIssuer = options.WorkloadAppIssuer;
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var clusterGroup = Environment.GetEnvironmentVariable("STRESS_CLUSTER_RESOURCE_GROUP");

            if (isLocal)
            {
                if (string.IsNullOrEmpty(options.Environment) || string.IsNullOrEmpty(options.LocalAddonsPath))
                {
                    Console.WriteLine("The --environment flag and --local-addons-path flags must be set when running locally.\n" +
                                      "Add '-e prod', '-e pg', or your custom environment to set the environment flag.\n" +
                                      "Add '-l <addons path>' to set the local addons path flag.\n" +
                                      "Local addons path can be found in <azure-sdk-tools repo>/tools/stress-cluster/cluster/kubernetes/stress-test-addons/.\n");
                    Environment.Exit(1);
                }

                using var reader = new StreamReader(Path.Combine(options.LocalAddonsPath, "values.yaml"));
                var yaml = new YamlStream();
                yaml.Load(reader);
                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

                if (String.IsNullOrEmpty(options.WorkloadAppIssuer))
                {
                    var workloadAppIssuerKey = (YamlMappingNode)mapping.Children[new YamlScalarNode("workloadAppIssuer")];
                    workloadAppIssuer = ((YamlScalarNode)workloadAppIssuerKey.Children[new YamlScalarNode(options.Environment)]).Value;
                }

                if (options.WorkloadAppPool == null)
                {
                    var workloadPoolKey = (YamlMappingNode)mapping.Children[new YamlScalarNode("workloadAppClientNamePool")];
                    var workloadPoolAppsCsv = ((YamlScalarNode)workloadPoolKey.Children[new YamlScalarNode(options.Environment)]).Value;
                    workloadAppPool = new List<string>(workloadPoolAppsCsv.Split(','));
                }

                var subscriptionKey = (YamlMappingNode)mapping.Children[new YamlScalarNode("subscriptionId")];
                subscriptionId = ((YamlScalarNode)subscriptionKey.Children[new YamlScalarNode(options.Environment)]).Value;

                var clusterGroupKey = (YamlMappingNode)mapping.Children[new YamlScalarNode("clusterGroup")];
                clusterGroup = ((YamlScalarNode)clusterGroupKey.Children[new YamlScalarNode(options.Environment)]).Value;
            }

            if (String.IsNullOrEmpty(workloadAppIssuer))
            {
                throw new Exception("Workload app issuer must be specified via --workload-app-issuer flag, or in addons values.yaml file if running locally");
            }
            if (workloadAppPool == null || workloadAppPool.Count == 0)
            {
                throw new Exception("Workload app pool must be specified via --workload-app-pool flag, or in addons values.yaml file if running locally");
            }
            if (String.IsNullOrEmpty(subscriptionId))
            {
                throw new Exception("Subscription must be specified via AZURE_SUBSCRIPTION_ID environment variable, or via --environment to load addons values.yaml file if running locally");
            }
            if (String.IsNullOrEmpty(clusterGroup))
            {
                throw new Exception("Cluster resource group must be specified via STRESS_CLUSTER_RESOURCE_GROUP environment variable, or via --environment to load addons values.yaml file if running locally");
            }

            return new WorkloadAuthConfig
            {
                WorkloadAppPool = workloadAppPool,
                WorkloadAppIssuer = workloadAppIssuer,
                SubscriptionId = subscriptionId,
                ClusterGroup = clusterGroup
            };
        }

        static async Task PollAndSyncCredentials(NamespaceEventHandler namespaceHandler, int minutes)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes));
                await namespaceHandler.SyncCredentials();
            }
        }
    }
}
