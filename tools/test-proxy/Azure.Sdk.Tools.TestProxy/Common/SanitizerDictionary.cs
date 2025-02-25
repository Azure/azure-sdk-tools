using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RegisteredSanitizer
    {
        public string Id { get; set; }
        public RecordedTestSanitizer Sanitizer { get; set; }

        public string Description { get; set; }

        public RegisteredSanitizer(RecordedTestSanitizer sanitizer, string id, string description = null)
        {
            Id = id;
            Sanitizer = sanitizer;
            Description = description;
            sanitizer.SanitizerId = id;
        }
    }

    public static class IdFactory
    {
        private static ulong CurrentId = 0;

        public static ulong GetNextId()
        {
            return Interlocked.Increment(ref CurrentId);
        }
    }

    public class SanitizerDictionary
    {
        public ConcurrentDictionary<string, RegisteredSanitizer> Sanitizers = new ConcurrentDictionary<string, RegisteredSanitizer>();

        // we have to know which sanitizers are session only
        // so that when we start a new recording we can properly
        // apply only the sanitizers that have been registered at the global level
        public List<string> SessionSanitizers = new List<string>();
        public SemaphoreSlim SessionSanitizerLock { get; set; } = new SemaphoreSlim(1);

        public SanitizerDictionary() {
            ResetSessionSanitizers().Wait();
        }

        /*
         * The below list has been grouped and labelled with some room for expansion. As such:
         *
         * General sanitizers  = 1XXX
         * Header sanitizers   = 2XXX
         * Body sanitizers     = 3XXX
         * URI, special, other = 4XXX
         *
         * */

        private const string EMPTYGUID = "00000000-0000-0000-0000-000000000000";
        private const string BASE64ZERO = "MA==";

        public List<RegisteredSanitizer> DefaultSanitizerList = new List<RegisteredSanitizer>
            {
                #region GeneralRegex
                // basic RecordedTestSanitizer handles Authorization header
                new RegisteredSanitizer(
                    new RecordedTestSanitizer(),
                    "AZSDK0000"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "SharedAccessKey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK1000"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "AccountKey=(?<key>[^;\\\"]+)", value: BASE64ZERO, groupForReplace: "key"),
                    "AZSDK1001"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "accesskey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK1002"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "Accesskey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK1003"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "Secret=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK1004"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "common/userrealm/(?<realm>[^/\\.]+)", groupForReplace: "realm"),
                    "AZSDK1005",
                    "ACS Identity leverages these strings to store identity information."
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "/identities/(?<realm>[^/?]+)", groupForReplace: "realm"),
                    "AZSDK1006",
                    "ACS Identity leverages these strings to store identity information."
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "(?:[?&](sig|sv)=)(?<secret>[^&\\\"\\s\\n,\\\\]+)", groupForReplace: "secret"),
                    "AZSDK1007",
                    "Common SAS URL Sanitizer. Applies to all headers, URIs, and text bodies."
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "token=(?<token>[^&\\\"\\s\\n,\\\\]+)", groupForReplace: "token"),
                    "AZSDK1008"
                ),
                #endregion
                #region HeaderRegex
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("api-key"),
                    "AZSDK2001"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-encryption-key"),
                    "AZSDK2002"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Location", value: "https://example.com"),
                    "AZSDK2003"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("subscription-key"),
                    "AZSDK2004"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("SupplementaryAuthorization"),
                    "AZSDK2005"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-rename-source"),
                    "AZSDK2006"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-file-rename-source"),
                    "AZSDK2007"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-copy-source"),
                    "AZSDK2008"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-copy-source-authorization"),
                    "AZSDK2009"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-file-rename-source-authorization"),
                    "AZSDK2010"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-encryption-key-sha256"),
                    "AZSDK2011"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-sas-token"),
                    "AZSDK2012"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-sas-key"),
                    "AZSDK2013"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-channel-name"),
                    "AZSDK2014"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Set-Cookie"),
                    "AZSDK2015"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Cookie"),
                    "AZSDK2016"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("client-request-id"),
                    "AZSDK2017"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("MS-CV"),
                    "AZSDK2018"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("X-Azure-Ref"),
                    "AZSDK2019"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-request-id"),
                    "AZSDK2020"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-client-request-id"),
                    "AZSDK2021"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-content-sha256"),
                    "AZSDK2022"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Content-Security-Policy-Report-Only"),
                    "AZSDK2023"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Repeatability-First-Sent"),
                    "AZSDK2024"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Repeatability-Request-ID"),
                    "AZSDK2025"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("repeatability-request-id"),
                    "AZSDK2026"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("repeatability-first-sent"),
                    "AZSDK2027"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("P3P"),
                    "AZSDK2028"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-ests-server"),
                    "AZSDK2029"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("operation-location", value: "https://example.com"),
                    "AZSDK2030"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Ocp-Apim-Subscription-Key"),
                    "AZSDK2031"
                ),
                #endregion
                #region BodyRegex
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(client_id=)(?<cid>[^&\\\"\\s\\n,\\\\]+)", groupForReplace: "cid"),
                    "AZSDK3000"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "client_secret=(?<secret>[^&\\\"\\s\\n,\\\\]+)", groupForReplace: "secret"),
                    "AZSDK3001"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "client_assertion=(?<secret>[^&\\\"\\s\\n,\\\\]+)", groupForReplace: "secret"),
                    "AZSDK3002"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "-----BEGIN PRIVATE KEY-----\\n(?<cert>.+\\n)*-----END PRIVATE KEY-----\\n", groupForReplace: "cert"),
                    "AZSDK3004"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).+?(?:<Value>)(?<group>.+)(?:</Value>)", groupForReplace: "group", value: BASE64ZERO),
                    "AZSDK3005"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).+?(?:<SignedTid>)(?<group>.+)(?:</SignedTid>)", groupForReplace: "group", value: EMPTYGUID),
                    "AZSDK3006"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).+?(?:<SignedOid>)(?<group>.+)(?:</SignedOid>)", groupForReplace: "group", value: EMPTYGUID),
                    "AZSDK3007"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:Password=)(?<pwd>.+?)(?:;)", groupForReplace: "pwd"),
                    "AZSDK3008"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:User ID=)(?<id>.+?)(?:;)", groupForReplace: "id"),
                    "AZSDK3009"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:<PrimaryKey>)(?<key>.+)(?:</PrimaryKey>)", groupForReplace: "key"),
                    "AZSDK3010"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:<SecondaryKey>)(?<key>.+)(?:</SecondaryKey>)", groupForReplace: "key"),
                    "AZSDK3011"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "<ClientIp>(?<secret>.+)</ClientIp>", groupForReplace: "secret"),
                    "AZSDK3012"
                ),
                #endregion
                #region BodyKey
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..access_token"),
                    "AZSDK3400"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..refresh_token"),
                    "AZSDK3401"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..containerUrl"),
                    "AZSDK3402"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..applicationSecret"),
                    "AZSDK3403"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..apiKey"),
                    "AZSDK3404"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..connectionString"),
                    "AZSDK3405"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..sshPassword"),
                    "AZSDK3406"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..aliasSecondaryConnectionString"),
                    "AZSDK3407"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryKey"),
                    "AZSDK3408"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryKey"),
                    "AZSDK3409"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..adminPassword.value"),
                    "AZSDK3410"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..administratorLoginPassword"),
                    "AZSDK3411"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accessToken"),
                    "AZSDK3412"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..runAsPassword"),
                    "AZSDK3413"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..adminPassword"),
                    "AZSDK3414"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accessSAS"),
                    "AZSDK3415"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..WEBSITE_AUTH_ENCRYPTION_KEY"),
                    "AZSDK3416"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..decryptionKey"),
                    "AZSDK3417"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..access_token"),
                    "AZSDK3418"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..AccessToken"),
                    "AZSDK3419"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..targetResourceId"),
                    "AZSDK3420"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..urlSource"),
                    "AZSDK3421"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..azureBlobSource.containerUrl"),
                    "AZSDK3422"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..source"),
                    "AZSDK3423"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..to"),
                    "AZSDK3424"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..from"),
                    "AZSDK3425"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..outputDataUri"),
                    "AZSDK3426"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..inputDataUri"),
                    "AZSDK3427"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..containerUri"),
                    "AZSDK3428"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..sasUri", regex: "sig=(?<sig>[^&]+)", groupForReplace: "sig"),
                    "AZSDK3429"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..id"),
                    "AZSDK3430"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..token"),
                    "AZSDK3431"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..appId"),
                    "AZSDK3432"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..userId"),
                    "AZSDK3433"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageAccount"),
                    "AZSDK3435"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..resourceGroup"),
                    "AZSDK3436"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..guardian"),
                    "AZSDK3437"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..scan"),
                    "AZSDK3438"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..catalog"),
                    "AZSDK3439"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..lastModifiedBy"),
                    "AZSDK3440"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..managedResourceGroupName"),
                    "AZSDK3441"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..createdBy"),
                    "AZSDK3442"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..tenantId", value: EMPTYGUID),
                    "AZSDK3443"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..principalId", value: EMPTYGUID),
                    "AZSDK3444"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..clientId", value: EMPTYGUID),
                    "AZSDK3445"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..credential"),
                    "AZSDK3446"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.key"),
                    "AZSDK3447"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.value[*].key"),
                    "AZSDK3448"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..uploadUrl"),
                    "AZSDK3449"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..logLink"),
                    "AZSDK3450"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerUri"),
                    "AZSDK3451"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerReadListSas"),
                    "AZSDK3452"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerWriteSas"),
                    "AZSDK3453"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryMasterKey"),
                    "AZSDK3454"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryReadonlyMasterKey"),
                    "AZSDK3455"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryMasterKey"),
                    "AZSDK3456"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryReadonlyMasterKey"),
                    "AZSDK3457"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..password"),
                    "AZSDK3458"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..certificatePassword"),
                    "AZSDK3459"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..clientSecret"),
                    "AZSDK3460"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..keyVaultClientSecret"),
                    "AZSDK3461"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountKey"),
                    "AZSDK3462"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..authHeader"),
                    "AZSDK3463"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..httpHeader"),
                    "AZSDK3464"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..encryptedCredential"),
                    "AZSDK3465"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..appkey"),
                    "AZSDK3466"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..functionKey"),
                    "AZSDK3467"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..atlasKafkaPrimaryEndpoint"),
                    "AZSDK3468"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..atlasKafkaSecondaryEndpoint"),
                    "AZSDK3469"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..certificatePassword"),
                    "AZSDK3470"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageAccountPrimaryKey"),
                    "AZSDK3471"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..privateKey"),
                    "AZSDK3472"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..fencingClientPassword"),
                    "AZSDK3473"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..acrToken"),
                    "AZSDK3474"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..scriptUrlSasToken"),
                    "AZSDK3475"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountKey"),
                    "AZSDK3477"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountName"),
                    "AZSDK3478"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..applicationId", value: EMPTYGUID),
                    "AZSDK3479"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..apiKey"),
                    "AZSDK3480"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..password"),
                    "AZSDK3482"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..userName"),
                    "AZSDK3483"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.WEBSITE_AUTH_ENCRYPTION_KEY"),
                    "AZSDK3484"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.siteConfig.machineKey.decryptionKey"),
                    "AZSDK3485"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.DOCKER_REGISTRY_SERVER_PASSWORD"),
                    "AZSDK3486"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..blob_sas_url"),
                    "AZSDK3487"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..targetResourceRegion"),
                    "AZSDK3488"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..domain_name"),
                    "AZSDK3489"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..etag"),
                    "AZSDK3490"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..functionUri"),
                    "AZSDK3491"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryConnectionString"),
                    "AZSDK3492"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..name"),
                    "AZSDK3493"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..friendlyName"),
                    "AZSDK3494"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..targetModelLocation"),
                    "AZSDK3495"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..resourceLocation"),
                    "AZSDK3496"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..keyVaultClientId", value: EMPTYGUID),
                    "AZSDK3497"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageAccountAccessKey"),
                    "AZSDK3498"
                ),
                #endregion
                #region UriRegex
                new RegisteredSanitizer(
                    new UriRegexSanitizer(regex: "sig=(?<sig>[^&]+)", groupForReplace: "sig"),
                    "AZSDK4000"
                ),
                new RegisteredSanitizer(
                    new UriRegexSanitizer(regex: "(?<=http://|https://)(?<host>[^/?\\.]+)", groupForReplace: "host"),
                    "AZSDK4001"
                ),
                #endregion
                #region RemoveHeader
                new RegisteredSanitizer(
                    new RemoveHeaderSanitizer("Telemetry-Source-Time"),
                    "AZSDK4003"
                ),
                new RegisteredSanitizer(
                    new RemoveHeaderSanitizer("Message-Id"),
                    "AZSDK4004"
                ),
            #endregion
        };

        /// <summary>
        /// Used to update the session sanitizers to their default configuration.
        /// </summary>
        public async Task ResetSessionSanitizers()
        {
            await SessionSanitizerLock.WaitAsync();
            try
            {
                var expectedSanitizers = DefaultSanitizerList;

                for (int i = 0; i < expectedSanitizers.Count; i++)
                {
                    var id = expectedSanitizers[i].Id;
                    var sanitizer = expectedSanitizers[i].Sanitizer;

                    if (!Sanitizers.ContainsKey(id))
                    {
                        _register(sanitizer, id);
                    }
                }

                SessionSanitizers = DefaultSanitizerList.Select(x => x.Id).ToList();
            }
            finally
            {
                SessionSanitizerLock.Release();
            }
        }

        /// <summary>
        /// Get the complete set of sanitizers that apply to this recording/playback session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public async Task<List<RecordedTestSanitizer>> GetSanitizers(ModifiableRecordSession session)
        {
            return (await GetRegisteredSanitizers(session)).Select(x => x.Sanitizer).ToList();
        }

        /// <summary>
        /// Gets a list of sanitizers that should be applied for the session level.
        /// </summary>
        /// <returns></returns>
        public async Task<List<RecordedTestSanitizer>> GetSanitizers()
        {
            return (await GetRegisteredSanitizers()).Select(x => x.Sanitizer).ToList();
        }

        /// <summary>
        /// Get the set of registered sanitizers for a specific recording or playback session.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public async Task<List<RegisteredSanitizer>> GetRegisteredSanitizers(ModifiableRecordSession session)
        {
            await session.Session.EntryLock.WaitAsync();
            try
            {
                var sanitizers = new List<RegisteredSanitizer>();

                foreach (var id in session.AppliedSanitizers)
                {
                    if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                    {
                        sanitizers.Add(sanitizer);
                    }
                    else
                    {
                        DebugLogger.LogError($"Failed to get a sanitizer with id {id}");
                    }
                }

                return sanitizers;
            }
            finally
            {
                session.Session.EntryLock.Release();
            }
        }

        /// <summary>
        /// Gets the set of registered sanitizers for the session level.
        /// </summary>
        /// <returns></returns>
        public async Task<List<RegisteredSanitizer>> GetRegisteredSanitizers()
        {
            var sanitizers = new List<RegisteredSanitizer>();
            await SessionSanitizerLock.WaitAsync();
            try
            {
                foreach (var id in SessionSanitizers)
                {
                    if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                    {
                        sanitizers.Add(sanitizer);
                    }
                }
            }
            finally
            {
                SessionSanitizerLock.Release();
            }

            return sanitizers;
        }

        private bool _register(RecordedTestSanitizer sanitizer, string id)
        {
            if (Sanitizers.TryAdd(id, new RegisteredSanitizer(sanitizer, id)))
            {
                return true;
            }
            else
            {
                // todo better error
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Unable to add sanitizer to global list.");
            }
        }

        /// <summary>
        /// Ensuring that session level sanitizers can be identified internally
        /// </summary>
        /// <param name="sanitizer">The sanitizer being registered</param>
        /// <param name="shouldLock"></param>
        /// <returns>The Id of the newly registered sanitizer.</returns>
        /// <exception cref="HttpException"></exception>
        public async Task<string> Register(RecordedTestSanitizer sanitizer, bool shouldLock = true)
        {
            var strCurrent = IdFactory.GetNextId().ToString();

            if (shouldLock)
            {
                await SessionSanitizerLock.WaitAsync();
            }

            try
            {
                if (_register(sanitizer, strCurrent))
                {
                    SessionSanitizers.Add(strCurrent);
                    return strCurrent;
                }
            }
            finally
            {
                if (shouldLock)
                {
                    SessionSanitizerLock.Release();
                }
            }
            throw new HttpException(System.Net.HttpStatusCode.InternalServerError, $"Unable to register global sanitizer id \"{strCurrent}\" with value '{JsonSerializer.Serialize(sanitizer)}'");
        }

        /// <summary>
        /// Register a sanitizer the global cache, add it to the set that applies to the session, and ensure we clean up after.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="sanitizer"></param>
        /// <param name="shouldLock"></param>
        /// <returns>The Id of the newly registered sanitizer.</returns>
        /// <exception cref="HttpException"></exception>
        public async Task<string> Register(ModifiableRecordSession session, RecordedTestSanitizer sanitizer, bool shouldLock = true)
        {
            var strCurrent = IdFactory.GetNextId().ToString();

            session.AuditLog.Enqueue(new AuditLogItem(session.SessionId, $"Starting registration of sanitizerId {strCurrent}"));

            if(shouldLock)
            {
                await session.Session.EntryLock.WaitAsync();
            }
            try
            {

                if (_register(sanitizer, strCurrent))
                {
                    session.AppliedSanitizers.Add(strCurrent);
                    session.ForRemoval.Add(strCurrent);

                    return strCurrent;
                }
            }
            finally
            {
                if (shouldLock)
                {
                    session.Session.EntryLock.Release();
                }
            }

            session.AuditLog.Enqueue(new AuditLogItem(session.SessionId, $"Finished registration of sanitizerId {strCurrent}"));
            return string.Empty;
        }

        /// <summary>
        /// Removes a sanitizer from the global session set.
        /// </summary>
        /// <param name="sanitizerId"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public async Task<string> Unregister(string sanitizerId)
        {
            await SessionSanitizerLock.WaitAsync();
            try
            {
                if (SessionSanitizers.Contains(sanitizerId))
                {
                    SessionSanitizers.Remove(sanitizerId);
                    return sanitizerId;
                }
            }
            finally
            {
                SessionSanitizerLock.Release();
            }

            return string.Empty;
        }

        /// <summary>
        /// Removes  a sanitizer from a specific recording or playback session.
        /// </summary>
        /// <param name="sanitizerId"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public async Task<string> Unregister(string sanitizerId, ModifiableRecordSession session)
        {
            await session.Session.EntryLock.WaitAsync();
            try
            {
                if (session.AppliedSanitizers.Contains(sanitizerId))
                {
                    session.AppliedSanitizers.Remove(sanitizerId);
                    return sanitizerId;
                }
            }
            finally
            {
                session.Session.EntryLock.Release();
            }
            session.AuditLog.Enqueue(new AuditLogItem(session.SessionId, $"Starting unregister of {sanitizerId}."));

            return string.Empty;
        }

        /// <summary>
        /// Fired at the end of a recording/playback session so that we can clean up the global dictionary.
        /// </summary>
        /// <param name="session"></param>
        public void Cleanup(ModifiableRecordSession session)
        {
            foreach(var sanitizerId in session.ForRemoval)
            {
                Sanitizers.TryRemove(sanitizerId, out var RemovedSanitizer);
            }
        }

        /// <summary>
        /// Not publically available via an API Route, but used to remove all of the active default session sanitizers.
        /// </summary>
        public async Task Clear()
        {
            await SessionSanitizerLock.WaitAsync();
            try
            {
                SessionSanitizers.Clear();
            }
            finally
            {
                SessionSanitizerLock.Release();
            }
        }
    }
}
