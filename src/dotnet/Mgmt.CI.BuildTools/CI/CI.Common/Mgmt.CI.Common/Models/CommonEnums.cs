using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MS.Az.Mgmt.CI.Common.Models
{
    /// <summary>
    /// Supported Github Repos
    /// The URL should not end with trailing with '/'
    /// </summary>
    public enum SupportedGitHubRepos
    {
        [Description("https://github.com/Azure/azure-sdk-for-net")]
        SdkForNet_PublicRepo,

        [Description("https://github.com/Azure/azure-sdk-for-net-pr")]
        SdkForNet_PrivateRepo,

        [Description("")]
        UnSupported
    }

}
