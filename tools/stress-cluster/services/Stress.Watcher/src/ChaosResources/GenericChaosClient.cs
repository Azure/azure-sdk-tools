using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s;

namespace Stress.Watcher
{
    public class GenericChaosClient
    {
        // Plural names for listing resource instances via Kubernetes API
        public enum ChaosResourcePlurals
        {
            networkchaos,
            stresschaos,
            httpchaos,
            iochaos,
            kernelchaos,
            timechaos,
            jvmchaos,
            schedules
        };

        public string Group = "chaos-mesh.org";
        public string Version = "v1alpha1";

        private List<GenericClient> Clients = new List<GenericClient>();

        public GenericChaosClient(KubernetesClientConfiguration config)
        {
            foreach (var plural in Enum.GetValues(typeof(ChaosResourcePlurals)))
            {
                Clients.Add(new GenericClient(config, Group, Version, plural.ToString()));
            }
        }

        public async Task<CustomResourceList<GenericChaosResource>> ListNamespacedAsync(string ns)
        {
            var tasks = Clients.Select(async cl => await cl.ListNamespacedAsync<CustomResourceList<GenericChaosResource>>(ns));
            await Task.WhenAll(tasks);

            var results = new CustomResourceList<GenericChaosResource>();
            results.Items = new List<GenericChaosResource>();

            foreach (var task in tasks)
            {
                results.Items.AddRange(task.Result.Items);
            }

            return results;
        }
    }
}
