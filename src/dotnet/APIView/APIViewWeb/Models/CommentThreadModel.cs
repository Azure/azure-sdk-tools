// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Linq;

namespace APIViewWeb.Models
{
    public class CommentThreadModel
    {
        public CommentThreadModel(string reviewId, string lineId, IEnumerable<CommentModel> comments)
        {
            ReviewId = reviewId;
            LineId = lineId;
            LineClass = comments.FirstOrDefault().SectionClass;
            Comments = comments.Where(c => !c.IsResolve);
            var resolveComment = comments.FirstOrDefault(c => c.IsResolve);
            IsResolved = resolveComment != null;
            ResolvedBy = resolveComment?.Username;
        }

        public string ReviewId { get; set; }
        public IEnumerable<CommentModel> Comments { get; set; }
        public string LineId { get; set; }
        public string LineClass { get; set; }
        public bool IsResolved { get; set; }
        public string ResolvedBy { get; set; }
    }
}
