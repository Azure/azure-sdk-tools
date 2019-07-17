using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace common.Services
{
    public class AzureDevOpsDirectHttpService
    {
        /// <summary>
        /// Minimal model for serializing request
        /// </summary>
        private class RequestModel
        {
            public string Query { get; set; }
            public string[] IdentityTypes = new string[] { "user", };
            // TODO: Trim this down
            public string[] Properties = new string[]
            {
                "DisplayName",
                "IsMru",
                "ScopeName",
                "SamAccountName",
                "Active",
                "SubjectDescriptor",
                "Department",
                "JobTitle",
                "Mail",
                "MailNickname",
                "PhysicalDeliveryOfficeName",
                "SignInAddress",
                "Surname",
                "Guest",
                "TelephoneNumber",
                "Description",
            };

            public string[] FilterByAncestorEntityIds = new string[] { };
            public string[] FilterByEntityIds = new string[] { };
            public Dictionary<string, object> Options = new Dictionary<string, object>()
            {
                { "MinResults", 40 },
                { "MaxResults", 40 },
            };

            /// <summary>
            /// 
            /// </summary>
            /// <param name="organization"></param>
            /// <param name="project"></param>
            /// <param name="query"></param>
            public RequestModel(string organization, string project, string query)
            {
                Query = query;

                Options.Add("CollectionScopeName", organization);
                Options.Add("ProjectScopeName", project);
            }

            /// <summary>
            /// Serializes the current object to a JSONstring
            /// </summary>
            /// <returns>JSON string</returns>
            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        /// <summary>
        /// Minimal model for parsing response
        /// </summary>
        private class ResponseModel
        {
            public ResponseResults[] Results { get; set; }

            public Guid GetId() => new Guid(Results.First().GetLocalId());
        }

        /// <summary>
        /// Minimal model for parsing response
        /// </summary>
        private class ResponseResults
        {
            public string QueryToken { get; set; }
            public Dictionary<string, object> Identities { get; set; }
            public string GetLocalId() => Identities["localId"] as string;
        }

        private const string AcceptHeader = "application/json;api-version=5.2-preview.1;excludeUrls=true";
        private const string ContentType = "application/json";

        private static HttpClient httpClient = new HttpClient();

        private readonly string baseUrl;
        private readonly string organization;
        private readonly string project;
        private readonly string authToken;

        public AzureDevOpsDirectHttpService(string baseUrl, string organization, string project, string authToken)
        {
            this.baseUrl = baseUrl;
            this.organization = organization;
            this.project = project;
            this.authToken = authToken;
        }

        public async Task<Guid> LookupAliasGuid(string alias)
        {
            var requestModel = new RequestModel(organization, project, alias);

            var acceptHeader = new MediaTypeWithQualityHeaderValue(AcceptHeader);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/_apis/IdentityPicker/Identities");
            request.Headers.Accept.Add(acceptHeader);
            request.Content = new StringContent(requestModel.ToJson(), Encoding.UTF8, ContentType);

            var response = await httpClient.SendAsync(request);



            throw new NotImplementedException();
        }

        

        private AuthenticationHeaderValue GetAuthHeader(string token)
        {
            var headerValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{token}:"));
            var result = new AuthenticationHeaderValue("Basic", headerValue);
            return result;
        }
    }
}
