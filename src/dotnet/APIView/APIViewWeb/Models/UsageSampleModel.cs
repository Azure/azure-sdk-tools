using System;
using System.Linq;
using Newtonsoft.Json;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.IO;

namespace APIViewWeb
{
    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string UsageSampleFileId { get; set; }

        public UsageSampleModel(string revId, string fileId)
        {
            ReviewId = revId;
            UsageSampleFileId = fileId;
        }

    }
}
