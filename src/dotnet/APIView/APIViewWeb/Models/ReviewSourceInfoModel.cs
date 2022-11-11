
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIViewWeb.Models
{
    public class ReviewSourceInfoModel
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public string SourceUrl { get; set; }
        public string Language { get; set; }

        public ReviewSourceInfoModel(string sourceUrl, string language)
        {
            SourceUrl = sourceUrl;
            Language = language;
        }

        public async Task SerializeAsync(Stream stream)
        {
            await JsonSerializer.SerializeAsync(
                stream,
                this,
                jsonSerializerOptions);
        }
    }
}
