using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class LanguageServiceInput
    {
        public string Service { get; set; }
        public string Project { get; set; }
        public IEnumerable<IDictionary<string, string>> PackageVersions { get; set; }
        public IEnumerable<LanguageServiceTestInfo> Tests { get; set; }

        private string _primaryPackage;
        public string PrimaryPackage
        {
            get
            {
                if (!string.IsNullOrEmpty(_primaryPackage))
                {
                    return _primaryPackage;
                }
                else if (PackageVersions.First().Count == 1)
                {
                    return PackageVersions.First().First().Key;
                }
                else
                {
                    throw new InvalidOperationException("Must set PrimaryPackageVersion if PackageVersions contains multiple packages");
                }
            }

            set
            {
                _primaryPackage = value;
            }
        }
    }
}
