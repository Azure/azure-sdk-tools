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
            LineClass = comments.FirstOrDefault().SectionClass;
            Comments = comments;
            var resolveComment = comments.FirstOrDefault(c => c.IsResolved);
            IsResolved = resolveComment != null;
            ResolvedBy = resolveComment?.CreatedBy;
        }

        public string ReviewId { get; set; }
        public IEnumerable<CommentItemModel> Comments { get; set; }
        public string LineId { get; set; }
        public string LineClass { get; set; }
        public bool IsResolved { get; set; }
        public string ResolvedBy { get; set; }
    }
}
