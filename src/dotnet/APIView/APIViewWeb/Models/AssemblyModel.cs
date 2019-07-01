using APIView;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class AssemblyModel
    {
        public AssemblyModel(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public int Id { get; set; }

        public string Name { get; set; }

        public AssemblyAPIV Assembly { get; set; }

        public AssemblyModel()
        {
            this.Name = "Empty Assembly";
        }

        public AssemblyModel(Stream stream, string fileName)
        {
            Assembly = AssemblyAPIV.AssemblyFromStream(stream);
            this.Name = fileName;
        }
    }
}
