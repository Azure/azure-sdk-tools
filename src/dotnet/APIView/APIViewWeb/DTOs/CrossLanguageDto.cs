using System.Collections.Generic;
using APIView.Model.V2;
using APIViewWeb.LeanModels;

namespace APIViewWeb.DTOs
{
    public class CrossLanguageDtoForApiParam
    {
        public string APIRevisionId { get; set; }
        public string APICodeFileId { get; set; }
    }

    public class CrossLanguageProcessingDto 
    {
        public Dictionary<string, List<CodePanelRowData>> Content { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        public bool GrabLines { get; set; }
        public int GrabIndent { get; set; } // Indent of parent line where grab started
        public ReviewLine CurrentRoot { get; set; } // Current Parent line whose descendant are being grabbed
        public CodePanelData CodePanelData { get; set; } = new CodePanelData(); // not used in the actual logic
    }

    public class CrossLanguageContentDto
    {
        public Dictionary<string, List<CodePanelRowData>> Content { get; set; } = new Dictionary<string, List<CodePanelRowData>>();
        public string APIRevisonId { get; set; }
    }
}
