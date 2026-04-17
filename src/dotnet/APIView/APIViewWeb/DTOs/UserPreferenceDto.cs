using APIViewWeb.LeanModels;
using System.Collections.Generic;

namespace APIViewWeb.DTOs
{
    public class UserPreferenceDto
    {
        public IEnumerable<string> Language { get; set; }
        public IEnumerable<APIRevisionType> APIRevisionType { get; set; }
        public IEnumerable<string> State { get; set; }
        public IEnumerable<string> Status { get; set; }
        public bool? HideLineNumbers { get; set; }
        public bool? HideLeftNavigation { get; set; }
        public bool? ShowHiddenApis { get; set; }
        public bool? ShowDocumentation { get; set; }
        public bool? HideReviewPageOptions { get; set; }
        public bool? HideIndexPageOptions { get; set; }
        public bool? HideSamplesPageOptions { get; set; }
        public bool? HideRevisionsPageOptions { get; set; }
        public bool? ShowComments { get; set; }
        public bool? ShowSystemComments { get; set; }
        public bool? DisableCodeLinesLazyLoading { get; set; }
        public string Theme { get; set; }
    }
}
