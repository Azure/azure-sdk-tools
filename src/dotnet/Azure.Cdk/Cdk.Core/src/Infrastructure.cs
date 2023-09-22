using Azure.Core.Serialization;
using Cdk.ResourceManager;

namespace Cdk.Core
{
    public abstract class Infrastructure
    {
        internal static readonly string Seed = Environment.GetEnvironmentVariable("AZURE_ENV_NAME") ?? throw new Exception("No environment variable found named 'AZURE_ENV_NAME'");

        private static Subscription? _defaultSubscription;
        public static Subscription DefaultSubscription => _defaultSubscription ??= new Subscription();

        public void ToBicep(string outputPath = ".")
        {
            outputPath = Path.GetFullPath(outputPath);
            foreach (var resource in Tenant.Instance.Resources)
            {
                if (resource is Subscription)
                {
                    foreach (var subscriptionChild in resource.Resources)
                    {
                        WriteBicepFile(outputPath, subscriptionChild);
                    }
                }
                else
                {
                    WriteBicepFile(outputPath, resource);
                }
            }
        }

        private string GetFilePath(string outputPath, Resource resource)
        {
            string fileName = resource is ResourceGroup ? Path.Combine(outputPath, "main.bicep") : Path.Combine(outputPath, "resources", resource.Name, $"{resource.Name}.bicep");
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
            return fileName;
        }

        private void WriteBicepFile(string outputPath, Resource resource)
        {
            using var stream = new FileStream(GetFilePath(outputPath, resource), FileMode.Create);
            stream.Write(ModelSerializer.Serialize(resource, "bicep"));
            if (resource is ResourceGroup)
            {
                foreach (var child in resource.Resources)
                {
                    WriteBicepFile(outputPath, child);
                }
            }
        }
    }
}
