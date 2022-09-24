using Newtonsoft.Json;
using System.Collections.Generic;

namespace APIViewWeb
{
    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public List<UsageSampleRevisionModel> Revisions { get; set; }

        public UsageSampleModel(string reviewId)
        {
            ReviewId = reviewId;
        }

    }
}
