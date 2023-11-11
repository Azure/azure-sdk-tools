using System.Collections.Generic;
using ApiView;
using APIView;
using APIViewWeb.Models;

namespace APIViewWeb.LeanModels
{
    public class ReviewContentModel
    {
        public ReviewListItemModel Review { get; set; }
        public NavigationItem[] Navigation { get; set; }
        public CodeLineModel[] codeLines { get; set; }
        public Dictionary<string, List<APIRevisionListItemModel>> APIRevisions { get; set; }
        public APIRevisionListItemModel ActiveRevision { get; set; }
        public APIRevisionListItemModel DiffRevision { get; set; }
        public int TotalActiveConversiations { get; set; }
        public int ActiveConversationsInActiveReviewRevision { get; set; }
        public int ActiveConversationsInSampleRevisions { get; set; }
        public HashSet<string> PreferredApprovers = new HashSet<string>();
        public HashSet<GithubUser> TaggableUsers { get; set; }
        public bool PageHasLoadableSections { get; set; }
    }
}
