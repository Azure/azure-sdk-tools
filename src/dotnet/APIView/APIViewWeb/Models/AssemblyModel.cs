using APIView;
using System.IO;

namespace APIViewWeb.Models
{
    public class AssemblyModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public AssemblyAPIV Assembly { get; set; }

        public AssemblyModel()
        {
            this.Name = "Empty Assembly";
        }

        public AssemblyModel(Stream stream, string fileName)
        {
            this.Assembly = AssemblyAPIV.AssemblyFromStream(stream);
            this.Name = fileName;
        }

        public AssemblyModel(AssemblyAPIV assembly, string fileName)
        {
            this.Assembly = assembly;
            this.Name = fileName;
        }
    }
}
