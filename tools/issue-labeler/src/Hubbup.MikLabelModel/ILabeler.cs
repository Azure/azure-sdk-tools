// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using IssueLabeler.Shared;
using System.Threading.Tasks;

namespace Hubbup.MikLabelModel
{
    public interface ILabeler
    {
        Task DispatchLabelsAsync(string owner, string repo, int number);
        Task<LabelSuggestion> PredictUsingModelsFromStorageQueue(string owner, string repo, int number);
    }
}