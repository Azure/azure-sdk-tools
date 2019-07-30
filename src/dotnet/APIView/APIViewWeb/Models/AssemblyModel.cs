using APIView;
using System;
using System.IO;

namespace APIViewWeb.Models
{
    public class AssemblyModel
    {
        public AssemblyAPIV Assembly { get; set; }
        public string Author { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime TimeStamp { get; set; }

        public AssemblyModel()
        {
            this.Name = "Empty Assembly";
        }

        public AssemblyModel(Stream stream, string fileName)
        {
            this.Assembly = AssemblyAPIV.AssemblyFromStream(stream);
            this.Name = fileName;
            this.TimeStamp = DateTime.UtcNow;
        }

        public AssemblyModel(AssemblyAPIV assembly, string fileName)
        {
            this.Assembly = assembly;
            this.Name = fileName;
            this.TimeStamp = DateTime.UtcNow;
        }
    }
}
