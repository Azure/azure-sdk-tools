using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    public class VersionSorter : NetSdkUtilTask, IComparer<Version>
    {
        public VersionSorter() { }

        public int Compare(Version x, Version y)
        {
            if (x > y)
            {
                return 1;
            }
            else if (x < y)
            {
                return -1;
            }
            else
                return 0;
        }
    }
}
