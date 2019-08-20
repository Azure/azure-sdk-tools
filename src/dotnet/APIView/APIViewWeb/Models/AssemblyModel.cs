using ApiView;
using System;
using System.Collections.ObjectModel;
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
        public bool HasOriginal { get; set; }
        public string OriginalFileName { get; set; }

        public AssemblyModel()
        {
        }

        public void BuildFromStream(Stream stream)
        {
            var assemblySymbol = AssemblyApiView.GetCompilation(stream);
            this.AssemblyNode = new CodeFileBuilder().Build(assemblySymbol);
            this.Name = assemblySymbol.Name;
        }
    }
}
