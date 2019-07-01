using APIView;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.IO;

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

        [Display(Name = "Display String")]
        public string DisplayString { get; set; }

        public string Name { get; set; }

        public AssemblyModel()
        {
            this.DisplayString = "<empty>";
            this.Name = "Empty Assembly";
        }

        public AssemblyModel(string dllPath, string fileName)
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssemblyFromFile(dllPath);
            
            if (assembly == null)
            {
                this.DisplayString = "<empty>";
            }
            else
            {
                this.DisplayString = assembly.ToString();
            }
            this.Name = fileName;
        }

        public AssemblyModel(Stream stream, string fileName)
        {
            AssemblyAPIV assembly = AssemblyAPIV.AssemblyFromStream(stream);
            if (assembly == null)
            {
                this.DisplayString = "<empty>";
            }
            else
            {
                this.DisplayString = assembly.ToString();
            }
            this.Name = fileName;
        }
    }
}
