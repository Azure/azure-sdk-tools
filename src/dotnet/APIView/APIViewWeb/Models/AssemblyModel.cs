using ApiView;
using APIView;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class AssemblyModel
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public AssemblyApiView Assembly { get; set; }
        public CodeFile AssemblyNode { get; set; }
        public string Author { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool HasOriginal { get; set; }
        public string OriginalFileName { get; set; }

        public AssemblyModel()
        {
        }

        public AnalysisResult[] BuildFromStream(Stream assemblyStream, bool runAnalysis)
        {
            var assemblySymbol = AssemblyApiView.GetCompilation(assemblyStream);
            AnalysisResult[] staticAnalysisResults;
            (this.AssemblyNode, staticAnalysisResults) = new CodeFileBuilder().Build(assemblySymbol, runAnalysis);
            this.Name = AssemblyNode.Name;

            return staticAnalysisResults;
        }

        public async Task BuildFromJsonAsync(Stream jsonStream)
        {
            this.AssemblyNode = await CodeFile.DeserializeAsync(jsonStream);
            this.Name = AssemblyNode.Name;
        }

        public static async Task<AssemblyModel> DeserializeAsync(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<AssemblyModel>(
                stream,
                JsonSerializerOptions);
        }

        public async Task SerializeAsync(Stream stream)
        {
            await JsonSerializer.SerializeAsync(
                stream,
                this,
                JsonSerializerOptions);
        }
    }
}
