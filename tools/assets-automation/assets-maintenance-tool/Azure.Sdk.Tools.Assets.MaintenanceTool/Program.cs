using System.ComponentModel.Design;
using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // all should honor
                // --ScanData <-- results from previous scans
            
            // SCAN
                // --configuration: -> path to file

            // BACKUP
                // --configuration provided?
                    // SCAN
                    // BACKUP
                    // as each tag is backed up, it is saved with suffix _backup

            // RESTORE
                // --input-tag <tag that has been stored away>

            // CLEANUP
                // --configuration provided?
                    // SCAN
                    // BACKUP
                    // CLEANUP
                        // each tag as found by configuration

                // --input-tag <tag on repo>?
                    // SCAN, BACKUP, and CLEANUP individual tag

            Console.WriteLine("Hello, World!");
        }
    }
}
