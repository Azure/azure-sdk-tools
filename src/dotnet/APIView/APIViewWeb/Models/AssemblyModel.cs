using ApiView;
using System;
using System.IO;

namespace APIViewWeb.Models
{
    public class AssemblyModel
    {
        public AssemblyApiView Assembly { get; set; }
        public CodeFile AssemblyNode { get; set; }
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
            this.AssemblyNode = new CodeFileBuilder().Build(AssemblyApiView.GetCompilation(stream));
            this.Name = fileName;
            this.TimeStamp = DateTime.UtcNow;
        }

        public AssemblyModel(AssemblyApiView assembly, string fileName)
        {
            this.Assembly = assembly;
            this.Name = fileName;
            this.TimeStamp = DateTime.UtcNow;
        }
    }
}
