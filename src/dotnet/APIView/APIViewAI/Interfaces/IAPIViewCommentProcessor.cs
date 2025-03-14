// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewAI.Model;

namespace APIViewAI.Interfaces
{
    public interface IAPIViewCommentProcessor
    {
        public Task<IList<CommentResponseModel>> DetectSimilarSuggestionsAsync(CommentRequestModel comment);
        public Task<IList<CommentResponseModel>> GenerateAIReviewComments(IList<CommentRequestModel> comments, string reviewText);
    }
}
