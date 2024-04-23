using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RegisteredSanitizer
    {
        public string Id { get; set; }
        public RecordedTestSanitizer Sanitizer { get; set; }

        public RegisteredSanitizer(RecordedTestSanitizer sanitizer, string id)
        {
            Id = id;
            Sanitizer = sanitizer;
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
        private ConcurrentDictionary<string, RegisteredSanitizer> Sanitizers = new ConcurrentDictionary<string, RegisteredSanitizer>();

        // we have to know which sanitizers are session only
        // so that when we start a new recording we can properly 
        // apply only the sanitizers that have been registered at the global level
        public List<string> SessionSanitizers = new List<string>();

        public SanitizerDictionary() {
            ResetSessionSanitizers();
        }

        public List<RegisteredSanitizer> DefaultSanitizerList = new List<RegisteredSanitizer>
            {
                // basic RecordedTestSanitizer handles Authorization header
                new RegisteredSanitizer(
                    new RecordedTestSanitizer(),
                    "AZSDK001"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..access_token"),
                    "AZSDK002"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..refresh_token"),
                    "AZSDK003"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "SharedAccessKey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK004"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "AccountKey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK005"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..containerUrl"),
                    "AZSDK006"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "accesskey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK007"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..applicationSecret"),
                    "AZSDK008"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..apiKey"),
                    "AZSDK009"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("api-key"),
                    "AZSDK010"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..connectionString"),
                    "AZSDK011"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "Accesskey=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK012"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "Secret=(?<key>[^;\\\"]+)", groupForReplace: "key"),
                    "AZSDK013"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-encryption-key"),
                    "AZSDK014"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..sshPassword"),
                    "AZSDK015"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..aliasSecondaryConnectionString"),
                    "AZSDK016"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryKey"),
                    "AZSDK017"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryKey"),
                    "AZSDK018"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..adminPassword.value"),
                    "AZSDK019"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..administratorLoginPassword"),
                    "AZSDK020"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accessToken"),
                    "AZSDK021"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..runAsPassword"),
                    "AZSDK022"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..adminPassword"),
                    "AZSDK023"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accessSAS"),
                    "AZSDK024"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..WEBSITE_AUTH_ENCRYPTION_KEY"),
                    "AZSDK025"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..decryptionKey"),
                    "AZSDK026"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("ServiceBusDlqSupplementaryAuthorization", regex: "(?:(sv|sig|se|srt|ss|sp)=)(?<secret>[^&\\\"]+)", groupForReplace: "secret"),
                    "AZSDK027"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("ServiceBusSupplementaryAuthorization", regex: "(?:(sv|sig|se|srt|ss|sp)=)(?<secret>[^&\\\"]+)", groupForReplace: "secret"),
                    "AZSDK028"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..access_token"),
                    "AZSDK029"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..AccessToken"),
                    "AZSDK030"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(client_id=)(?<cid>[^&]+)", groupForReplace: "cid"),
                    "AZSDK031"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "client_secret=(?<secret>[^&\\\"]+)", groupForReplace: "secret"),
                    "AZSDK032"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "client_assertion=(?<secret>[^&\\\"]+)", groupForReplace: "secret"),
                    "AZSDK033"
                ),
                // new BodyKeySanitizer("$..targetModelLocation"), disabled, not a secret?
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..targetResourceId"),
                    "AZSDK034"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..urlSource"),
                    "AZSDK035"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..azureBlobSource.containerUrl"),
                    "AZSDK036"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..source"),
                    "AZSDK037"
                ),
                // new BodyKeySanitizer("$..resourceLocation"), disabled, not a secret?
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Location", value: "https://example.com"),
                    "AZSDK038"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..to"),
                    "AZSDK039"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..from"),
                    "AZSDK040"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("subscription-key"),
                    "AZSDK041"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..outputDataUri"),
                    "AZSDK042"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..inputDataUri"),
                    "AZSDK043"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..containerUri"),
                    "AZSDK044"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..sasUri"),
                    "AZSDK045"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:(sv|sig|se|srt|ss|sp)=)(?<secret>[^&\\\"\\s]*)", groupForReplace: "secret"),
                    "AZSDK046"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..id"),
                    "AZSDK047"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..token"),
                    "AZSDK048"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..appId"),
                    "AZSDK049"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..userId"),
                    "AZSDK050"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..id"),
                    "AZSDK051"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageAccount"),
                    "AZSDK052"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..resourceGroup"),
                    "AZSDK053"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..guardian"),
                    "AZSDK054"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..scan"),
                    "AZSDK055"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..catalog"),
                    "AZSDK056"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..lastModifiedBy"),
                    "AZSDK057"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..managedResourceGroupName"),
                    "AZSDK058"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..createdBy"),
                    "AZSDK059"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..tenantId"),
                    "AZSDK060"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..principalId"),
                    "AZSDK061"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..clientId"),
                    "AZSDK062"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..credential"),
                    "AZSDK063"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("SupplementaryAuthorization"),
                    "AZSDK064"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.key"),
                    "AZSDK065"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.value[*].key"),
                    "AZSDK066"
                ),
                new RegisteredSanitizer(
                    new UriRegexSanitizer(regex: "sig=(?<sig>[^&]+)", groupForReplace: "sig"),
                    "AZSDK067"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-rename-source"),
                    "AZSDK068"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-file-rename-source"),
                    "AZSDK069"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-copy-source"),
                    "AZSDK070"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-copy-source-authorization"),
                    "AZSDK071"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-file-rename-source-authorization"),
                    "AZSDK072"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-encryption-key-sha256"),
                    "AZSDK073"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..uploadUrl"),
                    "AZSDK074"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..logLink"),
                    "AZSDK075"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-sas-token"),
                    "AZSDK076"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-sas-key"),
                    "AZSDK077"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("aeg-channel-name"),
                    "AZSDK078"
                ),  
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerUri"),
                    "AZSDK079"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerReadListSas"),
                    "AZSDK080"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageContainerWriteSas"),
                    "AZSDK081"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "token=(?<token>[^&]+)($|&)", groupForReplace: "token"),
                    "AZSDK082"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "-----BEGIN PRIVATE KEY-----\\n(?<cert>.+\\n)*-----END PRIVATE KEY-----\\n", groupForReplace: "cert"),
                    "AZSDK083"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryMasterKey"),
                    "AZSDK084"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..primaryReadonlyMasterKey"),
                    "AZSDK085"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryMasterKey"),
                    "AZSDK086"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryReadonlyMasterKey"),
                    "AZSDK087"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..password"),
                    "AZSDK088"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..certificatePassword"),
                    "AZSDK089"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..clientSecret"),
                    "AZSDK090"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..keyVaultClientSecret"),
                    "AZSDK091"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountKey"),
                    "AZSDK092"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..authHeader"),
                    "AZSDK093"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..httpHeader"),
                    "AZSDK094"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..encryptedCredential"),
                    "AZSDK095"
                ),
                    new RegisteredSanitizer(
                    new BodyKeySanitizer("$..appkey"),
                    "AZSDK096"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..functionKey"),
                    "AZSDK097"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..atlasKafkaPrimaryEndpoint"),
                    "AZSDK098"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..atlasKafkaSecondaryEndpoint"),
                    "AZSDK099"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..certificatePassword"),
                    "AZSDK100"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..storageAccountPrimaryKey"),
                    "AZSDK101"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..privateKey"),
                    "AZSDK102"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..fencingClientPassword"),
                    "AZSDK103"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..acrToken"),
                    "AZSDK104"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..scriptUrlSasToken"),
                    "AZSDK105"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..refresh_token"),
                    "AZSDK106"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).*?(?:<Value>)(?<group>.*)(?:</Value>)", groupForReplace: "group"),
                    "AZSDK107"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).*?(?:<SignedTid>)(?<group>.*)(?:</SignedTid>)", groupForReplace: "group"),
                    "AZSDK108"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?<=<UserDelegationKey>).*?(?:<SignedOid>)(?<group>.*)(?:</SignedOid>)", groupForReplace: "group"),
                    "AZSDK109"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:Password=)(?<pwd>.*?)(?:;)", groupForReplace: "pwd"),
                    "AZSDK110"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:User ID=)(?<id>.*?)(?:;)", groupForReplace: "id"),
                    "AZSDK111"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:<PrimaryKey>)(?<key>.*)(?:</PrimaryKey>)", groupForReplace: "key"),
                    "AZSDK112"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "(?:<SecondaryKey>)(?<key>.*)(?:</SecondaryKey>)", groupForReplace: "key"),
                    "AZSDK113"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountKey"),
                    "AZSDK114"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..accountName"),
                    "AZSDK115"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..applicationId"),
                    "AZSDK116"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..apiKey"),
                    "AZSDK117"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..connectionString"),
                    "AZSDK118"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..password"),
                    "AZSDK119"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..userName"),
                    "AZSDK121"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.WEBSITE_AUTH_ENCRYPTION_KEY"),
                    "AZSDK122"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.siteConfig.machineKey.decryptionKey"),
                    "AZSDK123"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$.properties.DOCKER_REGISTRY_SERVER_PASSWORD"),
                    "AZSDK124"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Set-Cookie"),
                    "AZSDK125"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Cookie"),
                    "AZSDK126"
                ),
                new RegisteredSanitizer(
                    new BodyRegexSanitizer(regex: "<ClientIp>(?<secret>.+)</ClientIp>", groupForReplace: "secret"),
                    "AZSDK127"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("client-request-id"),
                    "AZSDK128"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..blob_sas_url"),
                    "AZSDK129"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..targetResourceRegion"),
                    "AZSDK130"
                ),
                new RegisteredSanitizer(
                    new RemoveHeaderSanitizer("Telemetry-Source-Time"),
                    "AZSDK131"
                ),
                new RegisteredSanitizer(
                    new RemoveHeaderSanitizer("Message-Id"),
                    "AZSDK132"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("MS-CV"),
                    "AZSDK133"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("X-Azure-Ref"),
                    "AZSDK134"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-request-id"),
                    "AZSDK135"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-client-request-id"),
                    "AZSDK136"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-content-sha256"),
                    "AZSDK137"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Content-Security-Policy-Report-Only"),
                    "AZSDK138"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Repeatability-First-Sent"),
                    "AZSDK139"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("Repeatability-Request-ID"),
                    "AZSDK140"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("repeatability-request-id"),
                    "AZSDK141"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("repeatability-first-sent"),
                    "AZSDK142"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("P3P"),
                    "AZSDK143"
                ),
                new RegisteredSanitizer(
                    new HeaderRegexSanitizer("x-ms-ests-server"),
                    "AZSDK144"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..domain_name"),
                    "AZSDK145"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "common/userrealm/(?<realm>[^/\\.]+)", groupForReplace: "realm"),
                    "AZSDK146"
                ),
                new RegisteredSanitizer(
                    new GeneralRegexSanitizer(regex: "/identities/(?<realm>[^/?]+)", groupForReplace: "realm"),
                    "AZSDK147"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..etag"),
                    "AZSDK148"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..functionUri"),
                    "AZSDK149"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..secondaryConnectionString"),
                    "AZSDK150"
                ),
                new RegisteredSanitizer(
                    new UriRegexSanitizer("REDACTED", regex: "(?<=http://|https://)(?<host>[^/?\\.]+)", groupForReplace: "host"),
                    "AZSDK151"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..name"),
                    "AZSDK152"
                ),
                new RegisteredSanitizer(
                    new BodyKeySanitizer("$..friendlyName"),
                    "AZSDK153"
                ),
                new RegisteredSanitizer(
                    new UriRegexSanitizer(regex: "(?:(sv|sig|se|srt|ss|sp)=)(?<secret>[^&\\\"\\s]*)", groupForReplace: "secret"),
                    "AZSDK154"
                ),
            };

        /// <summary>
        /// Used to update the session sanitizers to their default configuration.
        /// </summary>
        public void ResetSessionSanitizers()
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

        /// <summary>
        /// Get the complete set of sanitizers that apply to this recording/playback session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public List<RecordedTestSanitizer> GetSanitizers(ModifiableRecordSession session)
        {
            return GetRegisteredSanitizers(session).Select(x => x.Sanitizer).ToList();
        }

        /// <summary>
        /// Gets a list of sanitizers that should be applied for the session level.
        /// </summary>
        /// <returns></returns>
        public List<RecordedTestSanitizer> GetSanitizers()
        {
            return GetRegisteredSanitizers().Select(x => x.Sanitizer).ToList();
        }

        /// <summary>
        /// Get the set of registered sanitizers for a specific recording or playback session.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public List<RegisteredSanitizer> GetRegisteredSanitizers(ModifiableRecordSession session)
        {
            var sanitizers = new List<RegisteredSanitizer>();
            foreach (var id in session.AppliedSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
            }

            return sanitizers;
        }

        /// <summary>
        /// Gets the set of registered sanitizers for the session level.
        /// </summary>
        /// <returns></returns>
        public List<RegisteredSanitizer> GetRegisteredSanitizers()
        {
            var sanitizers = new List<RegisteredSanitizer>();
            foreach (var id in SessionSanitizers)
            {
                if (Sanitizers.TryGetValue(id, out RegisteredSanitizer sanitizer))
                {
                    sanitizers.Add(sanitizer);
                }
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
        /// <param name="sanitizer"></param>
        /// <returns>The Id of the newly registered sanitizer.</returns>
        /// <exception cref="HttpException"></exception>
        public string Register(RecordedTestSanitizer sanitizer)
        {
            var strCurrent = IdFactory.GetNextId().ToString();

            if (_register(sanitizer, strCurrent))
            {
                SessionSanitizers.Add(strCurrent);
                return strCurrent;
            }
            throw new HttpException(System.Net.HttpStatusCode.InternalServerError, $"Unable to register global sanitizer id \"{strCurrent}\" with value '{JsonSerializer.Serialize(sanitizer)}'");
        }

        /// <summary>
        /// Register a sanitizer the global cache, add it to the set that applies to the session, and ensure we clean up after.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="sanitizer"></param>
        /// <returns>The Id of the newly registered sanitizer.</returns>
        /// <exception cref="HttpException"></exception>
        public string Register(ModifiableRecordSession session, RecordedTestSanitizer sanitizer)
        {
            var strCurrent = IdFactory.GetNextId().ToString();
            if (_register(sanitizer, strCurrent))
            {
                session.AppliedSanitizers.Add(strCurrent);
                session.ForRemoval.Add(strCurrent);

                return strCurrent;
            }

            return string.Empty;
        }

        /// <summary>
        /// Removes a sanitizer from the global session set.
        /// </summary>
        /// <param name="sanitizerId"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public string Unregister(string sanitizerId)
        {
            if (SessionSanitizers.Contains(sanitizerId))
            {
                SessionSanitizers.Remove(sanitizerId);
                return sanitizerId;
            }

            throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"The requested sanitizer for removal \"{sanitizerId}\" is not active at the session level.");
        }

        /// <summary>
        /// Removes  a sanitizer from a specific recording or playback session.
        /// </summary>
        /// <param name="sanitizerId"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        /// <exception cref="HttpException"></exception>
        public string Unregister(string sanitizerId, ModifiableRecordSession session)
        {
            if (session.AppliedSanitizers.Contains(sanitizerId))
            {
                session.AppliedSanitizers.Remove(sanitizerId);
                return sanitizerId;
            }

            throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"The requested sanitizer for removal \"{sanitizerId}\" is not active on recording/playback with id \"{session.SessionId}\".");
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
        public void Clear()
        {
            SessionSanitizers.Clear();
        }
    }
}
