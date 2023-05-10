using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    public class AssetsScanResultSet
    {
        public List<AssetsScanResult> Results { get; set; } = new List<AssetsScanResult>();

        public Dictionary<string, List<AssetsScanResult>> ByRepo { get; private set; }

        public Dictionary<string, List<AssetsScanResult>> ByTargetTag { get; private set; }

        public Dictionary<string, List<AssetsScanResult>> ByOriginSHA { get; private set; }

        public AssetsScanResultSet(List<AssetsScanResult> input) {
            Results = input;
            ByRepo = new Dictionary<string, List<AssetsScanResult>>();
            ByTargetTag = new Dictionary<string, List<AssetsScanResult>>();
            ByOriginSHA = new Dictionary<string, List<AssetsScanResult>>();

            foreach (var result in Results)
            {
                if (!ByRepo.ContainsKey(result.Repo))
                {
                    ByRepo.Add(result.Repo, new List<AssetsScanResult>());
                }
                ByRepo[result.Repo].Add(result);

                if (!ByTargetTag.ContainsKey(result.Tag))
                {
                    ByTargetTag.Add(result.Tag, new List<AssetsScanResult>());
                }
                ByTargetTag[result.Tag].Add(result);

                if (!ByOriginSHA.ContainsKey(result.Commit))
                {
                    ByOriginSHA.Add(result.Commit, new List<AssetsScanResult>());
                }
                ByOriginSHA[result.Commit].Add(result);
            }
        }
    }
}
