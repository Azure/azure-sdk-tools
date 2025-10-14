// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Models
{
    public class CommentThreadModel
    {
        public CommentThreadModel(string reviewId, string lineId, IEnumerable<CommentItemModel> comments)
        {
            ReviewId = reviewId;
            LineId = lineId;
            CrossLanguageId = comments.FirstOrDefault().CrossLanguageId;
            LineClass = comments.FirstOrDefault().SectionClass;
            Comments = comments;
            var resolveComment = comments.FirstOrDefault(c => c.IsResolved);
            IsResolved = resolveComment != null;
            // Find who resolved the thread by looking at the change history for the 'Resolved' action
            // Look through all comments to find the most recent resolution action
            ResolvedBy = comments
                .Where(c => c.IsResolved && c.ChangeHistory != null)
                .SelectMany(c => c.ChangeHistory.Where(h => h.ChangeAction == CommentChangeAction.Resolved))
                .OrderByDescending(h => h.ChangedOn)
                .FirstOrDefault()?.ChangedBy ?? resolveComment?.CreatedBy;
        }

        public string ReviewId { get; set; }
        public IEnumerable<CommentItemModel> Comments { get; set; }
        public string LineId { get; set; }
        public string CrossLanguageId { get; set; }
        public string LineClass { get; set; }
        public bool IsResolved { get; set; }
        public string ResolvedBy { get; set; }
    }
}
