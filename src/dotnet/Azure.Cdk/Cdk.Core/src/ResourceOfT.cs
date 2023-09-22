using Azure.Core;
using Azure.ResourceManager.Models;

namespace Cdk.Core
{
    public abstract class Resource<T> : Resource
        where T : notnull
    {
        public new T Properties { get; }

        protected Resource(Resource? scope, string resourceName, ResourceType resourceType, string version, T properties)
            : base(scope, resourceName, resourceType, version, properties)
        {
            Properties = properties;
        }
    }
}
