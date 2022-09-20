using System;
using System.Collections.Generic;
using System.IO;

namespace Stress.Generator
{
    // This class is a type wrapper around the chaos-mesh networkchaos type:
    // https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes
    // https://github.com/chaos-mesh/chaos-mesh/blob/master/api/v1alpha1/networkchaos_types.go
    public class NetworkChaos : Resource, IResource
    {
        public const string NetworkChaosDocSite = "https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes";

        public abstract class NetworkChaosAction : Resource, IResource
        {
            public override string ToString()
            {
                return GetType().Name.Replace("Action", "").ToLower();
            }
        }

        public class LossAction : NetworkChaosAction
        {
            public override string Help { get; set; } = $"Configuration for network loss. See {NetworkChaosDocSite}/#loss";

            public override string Template { get; set; } = @"
  # (( Help ))
  action: loss
  loss:
    loss: (( Loss ))
    correlation: (( Correlation ))
";

            [ResourceProperty("Probability of packet loss, e.g. '0.5'.")]
            public double Loss { get; set; }

            [OptionalResourceProperty("Correlation between the current loss and the previous one, e.g. '0.5'.")]
            public double? Correlation { get; set; }
        }

        public class DelayAction : NetworkChaosAction {
            public override string Help { get; set; } = $"Configuration for packet delay. See {NetworkChaosDocSite}/#delay";

            public override string Template { get; set; } = @"
  # (( Help ))
  action: delay
  delay:
    latency: (( Latency ))
    correlation: (( Correlation ))
    jitter: (( Jitter ))
(( Reorder ))
";

            [ResourceProperty("Network latency, e.g. '50ms'")]
            public string? Latency { get; set; }

            [OptionalResourceProperty("Correlation between the current latency and the previous one, e.g. '0.5'.")]
            public double? Correlation { get; set; }

            [OptionalResourceProperty("Range of the network latency.")]
            public double? Jitter { get; set; }

            [OptionalNestedResourceProperty("Network packet reordering.", new Type[]{typeof(ReorderSpec)})]
            public ReorderSpec? Reorder { get; set; }
        }

        public class ReorderSpec : Resource, IResource
        {
            public override string Help { get; set; } = $"Configuration for packet reordering. See {NetworkChaosDocSite}/#reorder";

            public override string Template { get; set; } = @"
    # (( Help ))
    reorder:
      gap: (( Gap ))
      reorder: (( Reorder ))
      correlation: (( Correlation ))
";

            [ResourceProperty("Gap before and after packet reordering.")]
            public int Gap { get; set; }

            [ResourceProperty("Probability of packet re-ordering.")]
            public double Reorder { get; set; }

            [OptionalResourceProperty("Correlation between the current re-order and the previous one, e.g. '0.5'.")]
            public double? Correlation { get; set; }
        }

        public class DuplicateAction : NetworkChaosAction {
            public override string Help { get; set; } = $"Configuration for packet duplication. See {NetworkChaosDocSite}/#duplicate";

            public override string Template { get; set; } = @"
  # (( Help ))
  action: duplicate
  duplicate:
    duplicate: (( Duplicate ))
    correlation: (( Correlation ))
";

            [ResourceProperty("Probability of packet duplication, e.g. '0.5'.")]
            public string? Duplicate { get; set; }

            [OptionalResourceProperty("Correlation between the current duplication and the previous one, e.g. '0.5'.")]
            public string? Correlation { get; set; }
        }

        public class CorruptAction : NetworkChaosAction {
            public override string Help { get; set; } = $"Configuration for packet corruption. See {NetworkChaosDocSite}/#corrupt";

            public override string Template { get; set; } = @"
  # (( Help ))
  action: corrupt
  corrupt:
    corrupt: (( Corrupt ))
    correlation: (( Correlation ))
";

            [ResourceProperty("Probability of packet corruption, e.g. '0.5'.")]
            public string? Corrupt { get; set; }

            [OptionalResourceProperty("Correlation between the current corruption and the previous one, e.g. '0.5'.")]
            public string? Correlation { get; set; }
        }

        public class BandwidthAction : NetworkChaosAction {
            public override string Help { get; set; } = $"Configuration for bandwidth restriction. See {NetworkChaosDocSite}/#bandwidth";

            public override string Template { get; set; } = @"
  # (( Help ))
  action: bandwidth
  bandwidth:
    rate: (( Rate ))
    limit: (( Limit ))
    peakrate: (( PeakRate ))
    minburst: (( MinBurst ))
";

            [ResourceProperty("Rate of bandwidth limit, e.g. '1mbps'. Available units: bps, kbps, mbps, gpbs, tpbs (bytes per second).")]
            public string? Rate { get; set; }

            [ResourceProperty("Number of bytes that can be queued (minimum 1).")]
            public int Limit { get; set; }

            [ResourceProperty("Max bytes that can be sent simultaneously (minimum 1).")]
            public int Buffer { get; set; }

            [OptionalResourceProperty("Max consumption of bucket. set if perfect millescond timescale shaping is needed.")]
            public int? PeakRate { get; set; }

            [OptionalResourceProperty("Size of peakrate bucket.")]
            public int? MinBurst { get; set; }
        }

        public static Dictionary<string, Type> DD { get; set; } = new Dictionary<string, Type>();

        public override string Template { get; set; } = @"
# (( Help ))
apiVersion: chaos-mesh.org/v1alpha1
kind: NetworkChaos
metadata:
  name: '(( Name ))-{{ .Release.Name }}-{{ .Release.Revision }}'
  namespace: {{ .Release.Namespace }}
spec:
  scheduler:
    cron: '(( Schedule ))'
  duration: '(( Duration ))'
  selector:
    labelSelectors:
      # This label must match the testInstance label of the pod(s) to target chaos against.
      testInstance: '(( Name ))-{{ .Release.Name }}-{{ .Release.Revision }}'
      chaos: 'true'
    namespaces:
      - {{ .Release.Namespace }}
  direction: (( Direction ))
  externalTargets: (( ExternalTargets ))
  mode: all  # target all matching pods
(( Action ))
";

        public override string Help { get; set; } = "Configuration for network chaos. See https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes/";

        public string? Name { get; set; }

        [ResourceProperty("A list of domains/CNAME records to apply network chaos to/from, for example 'servicebus.windows.net'")]
        public List<string>? ExternalTargets { get; set; }

        [ResourceProperty("Packet direction. Options: 'to', 'from', 'both'")]
        public string? Direction { get; set; }

        [OptionalResourceProperty("Frequency with which the network chaos should run, e.g. '@every 30s' or a valid cron expression. See also https://chaos-mesh.org/docs/define-scheduling-rules/#schedule-field")]
        public string? Schedule { get; set; }

        [OptionalResourceProperty("Duration of the network chaos, e.g. '12s'. Set this if a cron schedule has been set")]
        public string? Duration { get; set; }

        [NestedResourceProperty(
          "Type of network chaos.",
          new Type[]{typeof(LossAction), typeof(DelayAction), typeof(DuplicateAction), typeof(CorruptAction), typeof(BandwidthAction)}
        )]
        public NetworkChaosAction? Action { get; set; }

        public override void Write()
        {
            Write(Path.Join("templates", $"{Name}-network-{Action}.yaml"));
        }

        public NetworkChaos() : base()
        {
        }
    }
}
