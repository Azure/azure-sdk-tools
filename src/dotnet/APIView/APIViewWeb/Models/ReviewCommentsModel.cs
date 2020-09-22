// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using APIViewWeb.Models;

namespace APIViewWeb
{
    public class ReviewCommentsModel
    {
        private Dictionary<string, CommentThreadModel> _threads;

        public ReviewCommentsModel(string reviewId, IEnumerable<CommentModel> comments)
        {
            _threads = comments.OrderBy(c => c.TimeStamp)
                .GroupBy(c => c.ElementId)
                .ToDictionary(c => c.Key ?? string.Empty, c => new CommentThreadModel(reviewId, c.Key, c));
        }

        public IEnumerable<CommentThreadModel> Threads => _threads.Values;

        public bool TryGetThreadForLine(string lineId, out CommentThreadModel threadModel)
        {
            threadModel = null;
            if (lineId == null)
            {
                return false;
            }

            return _threads.TryGetValue(lineId, out threadModel);
        }
    }
}