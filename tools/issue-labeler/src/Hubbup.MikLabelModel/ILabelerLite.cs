// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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