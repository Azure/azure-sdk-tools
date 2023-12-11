using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class ConversationModel : PageModel
    {
        private readonly ICommentsManager _commentsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly ISamplesRevisionsManager _samplesManager;
        private const string ENDPOINT_SETTING = "Endpoint";
        private readonly IBlobCodeFileRepository _codeFileRepository;
        public readonly UserPreferenceCache _preferenceCache;

        public string Endpoint { get; }
        public ReviewListItemModel Review { get; private set; }
        public APIRevisionListItemModel LatestAPIRevision { get; set; }
        public IEnumerable<SamplesRevisionModel> SamplesRevisions { get; private set; }
        public IEnumerable<List<string>> SampleLines { get; private set; }

        public IOrderedEnumerable<KeyValuePair<APIRevisionListItemModel, List<CommentThreadModel>>> Threads { get; set; }
        public Dictionary<(SamplesRevisionModel sampleRevision, int sampleRevisionNumber), List<CommentThreadModel>> UsageSampleThreads { get; set; }
        public HashSet<GithubUser> TaggableUsers { get; set; }
        public ConversationModel(
            IConfiguration configuration,
            IBlobCodeFileRepository codeFileRepository,
            ICommentsManager commentsManager,
            IReviewManager reviewManager,
            IAPIRevisionsManager apiRevisionsManager,
            UserPreferenceCache preferenceCache,
            ISamplesRevisionsManager samplesManager)
        {
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            _apiRevisionsManager = apiRevisionsManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _preferenceCache = preferenceCache;
            _samplesManager = samplesManager;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TaggableUsers = _commentsManager.GetTaggableUsers();
            TempData["Page"] = "conversation";
            Review = await _reviewManager.GetReviewAsync(User, id);
            LatestAPIRevision = await _apiRevisionsManager.GetLatestAPIRevisionsAsync(Review.Id);
            SamplesRevisions = await _samplesManager.GetSamplesRevisionsAsync(id);
            var comments = await _commentsManager.GetReviewCommentsAsync(id);
            Threads = await ParseThreads(comments.Threads);
            UsageSampleThreads = ParseUsageSampleThreads(comments.Threads);
            SampleLines = await getUsageSampleLines();
            return Page();
        }

        private async Task<IOrderedEnumerable<KeyValuePair<APIRevisionListItemModel, List<CommentThreadModel>>>> ParseThreads(IEnumerable<CommentThreadModel> threads)
        {
            var threadDict = new Dictionary<APIRevisionListItemModel, List<CommentThreadModel>>();
            var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(Review.Id);

            foreach (var thread in threads)
            {
                APIRevisionListItemModel lastRevisionForThread = null;
                DateTime lastRevisionDate = DateTime.MinValue;

                foreach (var comment in thread.Comments)
                {
                    if (comment.APIRevisionId == null)
                    {
                        continue;
                    }

                    APIRevisionListItemModel commentRevision = revisions.SingleOrDefault(r => r.Id == comment.APIRevisionId);

                    if (commentRevision == null)
                    {
                        // if revision that comment was added in has been deleted
                        continue;
                    }
                    var commentRevisionDate = commentRevision.CreatedOn;
                    // Group each thread under the last revision where a comment was added for it. 
                    if (commentRevisionDate >= lastRevisionDate)
                    {
                        lastRevisionDate = (DateTime)commentRevisionDate;
                        lastRevisionForThread = commentRevision;
                    }
                }
                if (lastRevisionForThread == null)
                {
                    continue;
                }
                if (!threadDict.ContainsKey(lastRevisionForThread))
                {
                    threadDict.Add(lastRevisionForThread, new List<CommentThreadModel>());
                }
                threadDict[lastRevisionForThread].Add(thread);
            }
            return threadDict.OrderByDescending(kvp => kvp.Key.CreatedOn);
        }

        private Dictionary<(SamplesRevisionModel sampleRevision, int sampleRevisionNumber), List<CommentThreadModel>> ParseUsageSampleThreads(IEnumerable<CommentThreadModel> threads)
        {
            var threadDict = new Dictionary<(SamplesRevisionModel sampleRevision, int sampleRevisionNumber), List<CommentThreadModel>>();
            
            if (!SamplesRevisions.Any())
            {
                return threadDict;
            }

            foreach (var thread in threads)
            {
                foreach (var comment in thread.Comments)
                {
                    if (comment.CommentType == CommentType.SampleRevision)
                    {
                        var index = SamplesRevisions.ToList().FindIndex(s => s.FileId.Equals(comment.ElementId.Split("-").First()));
                        var sampleRevision = SamplesRevisions.ElementAt(index);
                        if (sampleRevision.IsDeleted)
                        {
                            continue;
                        }

                        if(!threadDict.ContainsKey((sampleRevision, index)))
                        {
                            threadDict.Add((sampleRevision, index), new List<CommentThreadModel>());
                        }
                        threadDict[(sampleRevision, index)].Add(thread);

                    }
                }
            }

            return threadDict;
        }

        private async Task<IEnumerable<List<string>>> getUsageSampleLines()
        {
            List<List<string>> lines = new List<List<string>>();

            if (!SamplesRevisions.Any())
            {
                return lines;
            }

            foreach (var revision in SamplesRevisions)
            {
                string rawContent = await _samplesManager.GetSamplesRevisionContentAsync(revision.FileId);
                if (rawContent == null)
                {
                    continue;
                }
                rawContent = rawContent.Trim();
                var sampleLines = new List<string>();
                string[] content = rawContent.Split("\n");

                for (int i = 0; i < content.Length; i++)
                {
                    string lineContent = content[i];
                    if (lineContent.StartsWith("<div class=\"code") || lineContent.StartsWith("<div style=") || lineContent.StartsWith("<div class=\"lang"))
                    {
                        lineContent = "";
                    }

                    lineContent = lineContent.Replace("<pre>", "").Replace("</pre>", "").Replace("</div>", "").Replace("\n", "");
                    if (lineContent.Trim() == "")
                    {
                        continue;
                    }

                    sampleLines.Add(lineContent);
                }

                lines.Add(sampleLines);
            }

            lines.Reverse();
            return lines;
        }
    }
}
