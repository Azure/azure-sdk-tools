// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using IssueLabeler.Shared;
using Octokit;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Hubbup.MikLabelModel
{
    public interface ILabelerLite
    {
        Task<List<string>> QueryLabelPrediction(int issueNumber, string title, string body, string issueUserLogin, string repositoryName, string repositoryOwnerName);
    }
}