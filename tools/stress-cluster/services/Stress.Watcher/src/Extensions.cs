using k8s.Models;

namespace Stress.Watcher.Extensions
{
    public static class Extensions
    {
        public static string NamespacedName(this V1Pod pod) => $"{pod.Namespace()}/{pod.Name()}";

        public static string TestInstance(this V1Pod pod)
        {
            var instance = "";
            pod.Metadata.Labels.TryGetValue("testInstance", out instance);
            return instance;
        }
    }
}
