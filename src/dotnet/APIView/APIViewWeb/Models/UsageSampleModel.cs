using Newtonsoft.Json;
using System.Security.Claims;

namespace APIViewWeb
{
    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public int RevisionNum { get; set; }
        public string UsageSampleFileId { get; set; }
        public string UsageSampleOriginalFileId { get; set; }
        public string Author { get; set; }
        public string RevisionTitle { get; set; }

        public UsageSampleModel(ClaimsPrincipal user, string reviewId, int revisionNum = 0)
        {
            if(user != null)
            {
                Author = user.GetGitHubLogin();
            }
            ReviewId = reviewId;
            RevisionNum = revisionNum;
        }

    }
}
