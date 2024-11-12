// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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