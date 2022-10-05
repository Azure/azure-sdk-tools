using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class ConversationModel : PageModel
    {
        private readonly CommentsManager _commentsManager;
        private readonly ReviewManager _reviewManager;
        private readonly UsageSampleManager _samplesManager;
        private const string ENDPOINT_SETTING = "Endpoint";
        private readonly BlobCodeFileRepository _codeFileRepository;
        public readonly UserPreferenceCache _preferenceCache;

        public string Endpoint { get; }
        public ReviewModel Review { get; private set; }
        public UsageSampleModel Sample { get; private set; }
        public IEnumerable<List<string>> SampleLines { get; private set; }

        public IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> Threads { get; set; }
        public Dictionary<UsageSampleRevisionModel, List<CommentThreadModel>> UsageSampleThreads { get; set; }
        public ConversationModel(
            IConfiguration configuration,
            BlobCodeFileRepository codeFileRepository,
            CommentsManager commentsManager,
            ReviewManager reviewManager,
            UserPreferenceCache preferenceCache,
            UsageSampleManager samplesManager)
        {
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _preferenceCache = preferenceCache;
            _samplesManager = samplesManager;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "conversation";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Sample = (await _samplesManager.GetReviewUsageSampleAsync(id)).FirstOrDefault();
            var comments = await _commentsManager.GetReviewCommentsAsync(id);
            Threads = ParseThreads(comments.Threads);
            UsageSampleThreads = ParseUsageSampleThreads(comments.Threads);
            SampleLines = await getUsageSampleLines();
            return Page();
        }

        private IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> ParseThreads(IEnumerable<CommentThreadModel> threads)
        {
            var threadDict = new Dictionary<ReviewRevisionModel, List<CommentThreadModel>>();

            foreach (var thread in threads)
            {
                ReviewRevisionModel lastRevisionForThread = null;
                int lastRevision = 0;
                foreach (var comment in thread.Comments)
                {
                    if (comment.RevisionId == null)
                    {
                        continue;
                    }
                    ReviewRevisionModel commentRevision = Review.Revisions.SingleOrDefault(r => r.RevisionId == comment.RevisionId);
                    if (commentRevision == null)
                    {
                        // if revision that comment was added in has been deleted
                        continue;
                    }
                    var commentRevisionIndex = commentRevision.RevisionNumber;
                    // Group each thread under the last revision where a comment was added for it. 
                    if (commentRevisionIndex >= lastRevision)
                    {
                        lastRevision = commentRevisionIndex;
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
            return threadDict.OrderByDescending(kvp => Review.Revisions.IndexOf(kvp.Key));
        }

        private Dictionary<UsageSampleRevisionModel, List<CommentThreadModel>> ParseUsageSampleThreads(IEnumerable<CommentThreadModel> threads)
        {
            var threadDict = new Dictionary<UsageSampleRevisionModel, List<CommentThreadModel>>();
            
            if (Sample == null)
            {
                return threadDict;
            }

            foreach (var thread in threads)
            {
                foreach (var comment in thread.Comments)
                {
                    if (comment.IsUsageSampleComment)
                    {
                        var sampleRevision = Sample.Revisions.Where(e => e.FileId.Equals(comment.ElementId.Split("-").First())).First();
                        if (sampleRevision.RevisionIsDeleted)
                        {
                            continue;
                        }

                        if(!threadDict.ContainsKey(sampleRevision))
                        {
                            threadDict.Add(sampleRevision, new List<CommentThreadModel>());
                        }
                        threadDict[sampleRevision].Add(thread);

                    }
                }
            }

            return threadDict;
        }

        private async Task<IEnumerable<List<string>>> getUsageSampleLines()
        {
            List<List<string>> lines = new List<List<string>>();

            if (Sample == null)
            {
                return lines;
            }

            foreach (var revision in Sample.Revisions.ToArray().Reverse())
            {
                string rawContent = await _samplesManager.GetUsageSampleContentAsync(revision.FileId);
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
