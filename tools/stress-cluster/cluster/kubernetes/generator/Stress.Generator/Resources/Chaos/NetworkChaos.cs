using System;
using System.Collections.Generic;
using System.IO;

namespace Stress.Generator
{
    public class NetworkChaos : Resource
    {
        public override string Template { get; set; } = @"
apiVersion: chaos-mesh.org/v1alpha1
kind: NetworkChaos
metadata:
  name: '{{ .Release.Name }}-{{ .Release.Revision }}'
  namespace: {{ .Release.Namespace }}
spec:
  action: loss
  direction: to
  externalTargets:
    - bing.com
  mode: one
  selector:
    labelSelectors:
      testInstance: 'packet-loss-{{ .Release.Name }}-{{ .Release.Revision }}'
      chaos: 'true'
    namespaces:
      - {{ .Release.Namespace }}
  loss:
    loss: '100'
    correlation: '100'
";

        public override string Help { get; set; } = "Configuration for network chaos. See https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes/";

        [ResourceProperty("Network Chaos Name")]
        public string Name { get; set; }

        [OptionalResourceProperty("loss, delay, netem, duplicate, corrupt, partition, bandwidth")]
        public string Action { get; set; } = "loss";

        [ResourceProperty("A domain/cname, like servicebus.windows.net")]
        public List<string> ExternalTargets { get; set; }

        public override void Write()
        {
            Write(Path.Join("templates", $"{Name}-network-chaos.yaml"));
        }

        public NetworkChaos() : base()
        {
        }
    }
}
