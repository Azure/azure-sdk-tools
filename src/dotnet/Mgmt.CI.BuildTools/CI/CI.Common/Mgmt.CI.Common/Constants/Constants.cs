// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common
{    
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    /// <summary>
    /// Constants used within the Build.Tasks library
    /// </summary>
    public static partial class CommonConstants
    {

        /// <summary>
        /// Constants, defaults used for Nuget Publish task
        /// </summary>
        public static partial class NugetDefaults
        {
            public const string NUGET_PATH = "nuget.exe";
            public const string NUGET_PUBLISH_URL = "https://api.nuget.org/v3/index.json";            

            //Since the new format has been changed to .snupkg for symbol packages, we can publish it to the same locaiton as nuget pacakge.
            //Nuget claims during pushing of nuget package it will detect symbols package and will push the symbols package
            //We are going to push it to the same location in case it fails to detect on multi nuget package publishing scenarios
            public const string NUGET_SYMBOL_PUBLISH_URL = "https://api.nuget.org/v3/index.json";

            //Old location that does not work anymore
            public const string LEGACY_NUGET_SYMBOL_PUBLISH_URL = "https://nuget.smbsrc.net";
            public const int NUGET_TIMEOUT = 60; //Seconds
            public const string DEFAULT_API_KEY = "1234";
            public const string SDK_NUGET_APIKEY_ENV = "NetSdkNugetApiKey";
        }

        /// <summary>
        /// Constants used for various build stage tasks
        /// </summary>
        public static partial class BuildStageConstant
        {
            public const string API_TAG_PROPERTYNAME = "AzureApiTag";
            public const string PROPS_APITAG_FILE_NAME = "AzSdk.RP.props";
            public const string PROPS_MULTIAPITAG_FILE_NAME = "AzSdk.MultiApi.RP.props";
            public const string PROPS_PROFILE_FILE_NAME = "AzSdk.Profiles.RP.props";
            public const string APIMAPTYPENAMETOSEARCH = "SdkInfo";
            public const string PROPERTYNAMEPREFIX = "ApiInfo_";
        }

        public static partial class NetSdkToolsCommonConstants
        {
            public static readonly Uri GitHubApiBaseUri = new Uri(@"https://api.github.com/repos/");
            public static readonly String GitHubPRsUrl = @"{0}/{1}/pulls/{2}/files?access_token={3}";
            public static readonly String GitHubAccessTokenValidationUrl = @"Azure/azure-sdk-for-net/pulls/4736/files?access_token={0}";
            public static readonly String RestApiSpecRepoGitHubCloneUrl = @"https://github.com/Azure/azure-rest-api-specs.git";
            public static readonly String NetSdkRepoGitHubCloneUrl = @"https://github.com/Azure/azure-sdk-for-net.git";
        }

        public static partial class Git
        {
            public const string GIT_MODULE_FILE_NAME = ".gitmodules";
        }

        public static partial class OSPlatform
        {
            public const string WIN_OS_NAME = "Windows";
            public const string LINUX_OS_NAME = "Linux";
            public const string MAC_OSX_OS_NAME = "MacOs";
        }

        public static partial class AzureAuth
        {
            public static string SToS_ClientId = "8247a9ab-0084-417c-b249-5ce3610753cd";
            public static string SToS_ClientSecret = "SPN-AdxSdkKVApp-Secret";
            public static string SToS_SPN_RedirectUrl = @"https://microsoft.com/AdxSdkKVApp";
            public static string SToS_SPNAppName = "AdxSdkKVApp";

            public static string AADAuthUriString = @"https://login.microsoftonline.com";
            public static string AADTokenAudienceUriString = @"https://management.core.windows.net/";

            public static string MSFTTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            public class KVInfo
            {
                public static string KVName = "SToS-KV";

                public static string KVAudience = @"https://vault.azure.net";
                public static string BaseUri = "https://stos-kv.vault.azure.net";
                
                public class Secrets
                {
                    public const string GH_AccTkn = @"OTlkZGI2ZTFjYjQwYzdhODljYzRlZjJmODgwYmQzYjZmMTI4MTMwZg==";
                    public const String GH_AdxSdkNetAcccesToken = @"https://stos-kv.vault.azure.net/secrets/GH-AdxSdkAcccesToken/3eec97e2e30b4f818e68fa051bc0377b";
                }

                public class Certs
                {
                    public static string SToSKVSelfSignCertKey = "SToS-KVAccessCert";
                    public static string SToSKVCertSubjectName = "CN=SwaggerToSdk-KVAccess-Cert";
                    public static string SToSKVCertSubjectSearchKeyWord = "SwaggerToSdk";
                }
            }
        }
    }
}