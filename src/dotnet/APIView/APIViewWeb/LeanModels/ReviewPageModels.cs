using System.Collections.Generic;
using ApiView;
using APIView;

namespace APIViewWeb.LeanModels
{
    public class ReviewContentModel
    {
        public NavigationItem[] Navigation { get; set; }
        public CodeLine[] codeLines { get; set; }
    }

    public class RevisionListItemModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CreationDate { get; set; }
        public List<CodeFileModel> Files { get; set; }
    }

    public class CodeFileModel
    {
        public string ReviewFileId { get; set; }
        public string Name { get; set; }
    }
}
