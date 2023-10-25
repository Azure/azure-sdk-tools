using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Constants
{
    public class OrgConstants
    {
        // Azure is both the Org and the owner of the various repositories
        public const string Azure = "Azure";
        // Everyone with write permission in the Azure org either needs to be a direct user
        // in the azure-sdk-write team or a user in a child team of azure-sdk-write. 
        public const string AzureSdkWriteTeam = "azure-sdk-write";
        public const string AzureOrgTeamConstant = $"{Azure}/";
    }
}
