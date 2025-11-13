using System.Collections.Generic;
using APIView.Model.V2;
using APIViewWeb.LeanModels;

namespace APIViewWeb.DTOs
{
    public class CrossLanguageProcessingDto 
    {
        public Dictionary<string, List<CodePanelRowData>> Content { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        public bool GrabLines { get; set; }
        public int GrabIndent { get; set; } // Indent of parent line where grab started
        public ReviewLine ContextEndLine { get; set; }
        public ReviewLine CurrentRoot { get; set; } // Current Parent line whose descendant are being grabbed
        public CodePanelData CodePanelData { get; set; } = new CodePanelData(); // not used in the actual logic
    }

    public class CrossLanguageContentDto
    {
        /// <summary>
        /// Key: Cross Language Line ID
        /// Value: Cross Language Row Data
        /// </summary>
        public Dictionary<string, List<CodePanelRowData>> Content { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        public string APIRevisionId { get; set; }
        public string Language { get; set; }
    }
}
