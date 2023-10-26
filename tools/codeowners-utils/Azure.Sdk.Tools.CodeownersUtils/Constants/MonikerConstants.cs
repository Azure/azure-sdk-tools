using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Constants
{
    /// <summary>
    /// The allowed Monikers in CODEOWNERS files.
    /// </summary>
    public class MonikerConstants
    {
        // Note: AzureSdkOwners and ServiceOwner aren't implemented yet. They're
        // opportunistically being added below with the move of the moniker constants.
        // https://github.com/Azure/azure-sdk-tools/issues/5945
        public const string AzureSdkOwners = "AzureSdkOwners";
        public const string MissingFolder = "/<NotInRepo>/";
        public const string PRLabel = "PRLabel";
        public const string ServiceLabel = "ServiceLabel";
        public const string ServiceOwners = "ServiceOwners";
    }
}
