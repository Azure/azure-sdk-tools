using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Tests
{
    public class GitTokenSkipAttribute : NUnitAttribute, IApplyToTest
    {
        public GitTokenSkipAttribute() { }

        public void ApplyToTest(Test test)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GIT_TOKEN")))
            {
                new IgnoreAttribute("Skipping this test. Within a CI run, and GIT_TOKEN is not set.").ApplyToTest(test);
            }
        }
    }
}
