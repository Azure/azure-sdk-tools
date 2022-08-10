using Newtonsoft.Json;
using System.Security.Claims;

namespace APIViewWeb
{
    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string UsageSampleFileId { get; set; }
        public string Author { get; set; }

        public UsageSampleModel(ClaimsPrincipal user, string revId, string fileId)
        {
            if(user != null)
            {
                Author = user.GetGitHubLogin();
            }
            ReviewId = revId;
            UsageSampleFileId = fileId;
        }

    }
}
