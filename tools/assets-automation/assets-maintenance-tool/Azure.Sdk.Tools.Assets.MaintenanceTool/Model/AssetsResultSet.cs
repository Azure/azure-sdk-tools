using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    public class AssetsResultSet
    {

        public AssetsResultSet(List<AssetsResult> input)
        {
            Results = input;
            CalculateObjects();
        }

        public AssetsResultSet(List<AssetsResult> input, AssetsResultSet? existingSet)
        {
            Results = input;

            // todo: coalesce input + existing set

            CalculateObjects();
        }

        public List<AssetsResult> Results { get; set; } = new List<AssetsResult>();

        public Dictionary<string, List<AssetsResult>> ByRepo { get; private set; } = new();

        public Dictionary<string, List<AssetsResult>> ByTargetTag { get; private set; } = new();

        public Dictionary<string, List<AssetsResult>> ByOriginSHA { get; private set; } = new();

        private void CalculateObjects()
        {
            ByRepo = new Dictionary<string, List<AssetsResult>>();
            ByTargetTag = new Dictionary<string, List<AssetsResult>>();
            ByOriginSHA = new Dictionary<string, List<AssetsResult>>();

            foreach (var result in Results)
            {
                if (!ByRepo.ContainsKey(result.Repo))
                {
                    ByRepo.Add(result.Repo, new List<AssetsResult>());
                }
                ByRepo[result.Repo].Add(result);

                if (!ByTargetTag.ContainsKey(result.Tag))
                {
                    ByTargetTag.Add(result.Tag, new List<AssetsResult>());
                }
                ByTargetTag[result.Tag].Add(result);

                if (!ByOriginSHA.ContainsKey(result.Commit))
                {
                    ByOriginSHA.Add(result.Commit, new List<AssetsResult>());
                }
                ByOriginSHA[result.Commit].Add(result);
            }
        }
    }
}
