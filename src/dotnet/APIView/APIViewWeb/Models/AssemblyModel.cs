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

        public string JsonSerialization { get; set; }

        public string Name { get; set; }

        public AssemblyModel()
        {
            this.JsonSerialization = "{}";
            this.Name = "Empty Assembly";
        }

        public AssemblyModel(string dllPath, string fileName)
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssemblyFromFile(dllPath);
            
            if (assembly == null)
            {
                this.JsonSerialization = "{}";
            }
            else
            {
                this.JsonSerialization = JsonSerializer.ToString(assembly);
            }
            this.Name = fileName;
        }

        public AssemblyModel(Stream stream, string fileName)
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssemblyFromStream(stream);
            if (assembly == null)
            {
                this.JsonSerialization = "{}";
            }
            else
            {
                this.JsonSerialization = JsonSerializer.ToString(assembly);
            }
            this.Name = fileName;
        }
    }
}
