using System;

namespace Stress.Generator
{
    public class NetworkChaos : Resource
    {
        [OptionalResourceProperty("loss, delay, netem, duplicate, corrupt, partition, bandwidth")]
        public string Action { get; set; } = "loss";

        [ResourceProperty("A domain/cname, like servicebus.windows.net")]
        public string ExternalTargets { get; set; }

        public NetworkChaos() : base()
        {
        }
    }
}
