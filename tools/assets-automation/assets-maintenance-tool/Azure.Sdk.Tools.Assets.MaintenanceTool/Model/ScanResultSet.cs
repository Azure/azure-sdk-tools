using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    public class ScanResultSet
    {
        public List<ScanResult> Results { get; set; } = new List<ScanResult>();

        public Dictionary<string, List<ScanResult>> ByRepo { get; private set; }

        public Dictionary<string, List<ScanResult>> ByTargetTag { get; private set; }

        public Dictionary<string, List<ScanResult>> ByOriginSHA { get; private set; }

        public ScanResultSet(List<ScanResult> input) {
            Results = input;
            ByRepo = new Dictionary<string, List<ScanResult>>();
            ByTargetTag = new Dictionary<string, List<ScanResult>>();
            ByOriginSHA = new Dictionary<string, List<ScanResult>>();

            foreach (var result in Results)
            {
                if (!ByRepo.ContainsKey(result.Repo))
                {
                    ByRepo.Add(result.Repo, new List<ScanResult>());
                }
                ByRepo[result.Repo].Add(result);

                if (!ByTargetTag.ContainsKey(result.Tag))
                {
                    ByTargetTag.Add(result.Tag, new List<ScanResult>());
                }
                ByTargetTag[result.Tag].Add(result);

                if (!ByOriginSHA.ContainsKey(result.Commit))
                {
                    ByOriginSHA.Add(result.Commit, new List<ScanResult>());
                }
                ByOriginSHA[result.Commit].Add(result);
            }
        }
    }
}
