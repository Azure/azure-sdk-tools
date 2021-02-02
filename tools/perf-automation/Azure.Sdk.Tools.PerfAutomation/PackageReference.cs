using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public class PackageReference
    {
        public PackageReference(string nameAtVersion)
        {
            var parts = nameAtVersion.Split('@');
            if (parts.Length == 2)
            {
                Name = parts[0];
                Version = new Version(parts[1]);
            }
            else
            {
                throw new ArgumentException(nameof(nameAtVersion));
            }
        }

        public string Name { get; private set;}
        public Version Version { get; private set; }
    }
}
