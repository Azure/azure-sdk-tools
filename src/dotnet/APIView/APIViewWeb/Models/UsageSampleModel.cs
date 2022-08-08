using System;
using System.Linq;
using Newtonsoft.Json;
using APIViewWeb.Models;
using APIViewWeb.Repositories;

namespace APIViewWeb
{
    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; } 
        public string SampleContent { get; set; }
        public string UsageSampleFileId { get; set; } = IdHelper.GenerateId(); 
        
        public UsageSampleModel (string revId, string sample) 
        {
            SampleId = revId;
            SampleContent = sample;
        }
    }
}
