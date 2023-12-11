// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb
{
    public class ReviewCommentsModel
    {
        private Dictionary<string, CommentThreadModel> _threads;

        public ReviewCommentsModel(string reviewId, IEnumerable<CommentItemModel> comments)
        {
            _threads = comments.OrderBy(c => c.CreatedOn)
                .GroupBy(c => c.ElementId)
                .ToDictionary(c => c.Key ?? string.Empty, c => new CommentThreadModel(reviewId, c.Key, c));
        }

        public IEnumerable<CommentThreadModel> Threads => _threads.Values;

        public bool TryGetThreadForLine(string lineId, out CommentThreadModel threadModel, bool hideCommentRows = false)
        {
            threadModel = null;
            if (lineId == null)
            {
                return false;
            }

            var result = _threads.TryGetValue(lineId, out threadModel);
            if (hideCommentRows && threadModel != null)
            {
                if (!String.IsNullOrEmpty(threadModel.LineClass) && !threadModel.LineClass.Contains("lvl_1_"))
                {
                    threadModel.LineClass = threadModel.LineClass + " d-none";
                }
            }
            return result;
        }
    }
}
