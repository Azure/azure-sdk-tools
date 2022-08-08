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
        public string SampleId { get; set; } 

        public string SampleContent { get; set; }

        public string UsageSampleFileId { get; set; } = IdHelper.GenerateId();

        public UsageSampleModel(string revId, Stream sampleContent)
        {
            SampleId = revId;
            if (sampleContent != null) 
            { 
                String content = sampleContent.ToString(); // Write to file later.
                SampleContent = parseMDtoHTML(content);
            }
            else
            {
                SampleContent = null;
            }
        }

        private string parseMDtoHTML(string md)
        {
            return md;
        }

    }
}
