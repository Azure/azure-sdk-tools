// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb
{
    public class ReviewCommentsModel
    {
        private Dictionary<string, List<CommentThreadModel>> _threads;

        public ReviewCommentsModel(string reviewId, IEnumerable<CommentItemModel> comments)
        {
            _threads = comments
                .GroupBy(c => c.ElementId ?? string.Empty)
                .ToDictionary(
                    lineGroup => lineGroup.Key,
                    lineGroup => lineGroup
                        .GroupBy(c => c.ThreadId ?? c.Id)
                        .Select(threadGroup => new CommentThreadModel(
                            reviewId,
                            lineGroup.Key,
                            threadGroup.Key,
                            threadGroup.OrderBy(c => c.CreatedOn)))
                        .OrderByDescending(t => t.Comments.Min(c => c.CreatedOn)) // Threads ordered by creation (oldest comment): newest threads first
                        .ToList()
                );
        }

        public IEnumerable<CommentThreadModel> Threads => _threads.Values.SelectMany(t => t);

        public bool TryGetThreadsForLine(string lineId, out List<CommentThreadModel> threadModels, bool hideCommentRows = false)
        {
            threadModels = null;
            if (lineId == null)
            {
                return false;
            }

            var result = _threads.TryGetValue(lineId, out threadModels);
            if (!hideCommentRows || threadModels == null)
            {
                return result;
            }

            foreach (var thread in threadModels)
            {
                if (!string.IsNullOrEmpty(thread.LineClass) && !thread.LineClass.Contains("lvl_1_"))
                {
                    thread.LineClass += " d-none";
                }
            }
            return result;
        }

        // Legacy method for backward compatibility - returns the first thread
        public bool TryGetThreadForLine(string lineId, out CommentThreadModel threadModel, bool hideCommentRows = false)
        {
            threadModel = null;
            if (TryGetThreadsForLine(lineId, out var threads, hideCommentRows))
            {
                threadModel = threads.FirstOrDefault();
                return threadModel != null;
            }
            return false;
        }
    }
}
