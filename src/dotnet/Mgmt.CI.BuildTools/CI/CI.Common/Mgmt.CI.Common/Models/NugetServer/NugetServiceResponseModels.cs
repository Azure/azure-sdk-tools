using System.Collections.Generic;

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Models.Nuget
{
    internal class WildSearchNugetPackageModel
    {
        public int TotalHits { get; set; }

        public List<string> data { get; set; }
    }

    internal class EnumeratePkgVersionModel
    {
        public List<string> data { get; set; }
    }
}
