using k8s.Models;

namespace k8s
{
    public static class Extensions
    {
        public static string NamespacedName(this V1Pod pod) => $"{pod.Name()}/{pod.Namespace()}";
    }
}
