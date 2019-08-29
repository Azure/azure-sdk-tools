using ApiView;
using APIView;
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
            this.Name = assemblySymbol.Name;

            return staticAnalysisResults;
        }
    }
}
