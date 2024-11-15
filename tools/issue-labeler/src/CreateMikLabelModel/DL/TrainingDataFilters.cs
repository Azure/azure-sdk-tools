// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CreateMikLabelModel.Models;
using Octokit;

namespace CreateMikLabelModel
{
    /// <summary>
    ///   The set of filters to apply when creating a training set.
    /// </summary>
    ///
    public class TrainingDataFilters
    {
        /// <summary>
        ///   The set of names identifying labels which must all be present on an issue
        ///   for it to be included in the training set.
        /// </summary>
        ///
        /// <value>If <c>null</c> or empty, issues will not require any specific labels.</value>
        ///
        public string[] RequiredIssueLabelNames { get; init; }

        /// <summary>
        ///   The set of names identifying labels which must all be present on a pull request
        ///   for it to be included in the training set.
        /// </summary>
        ///
        /// <value>If <c>null</c> or empty, pull requests will not require any specific labels.</value>
        ///
        public string[] RequiredPullRequestLabelNames { get; init; }

        /// <summary>
        ///   Indicates whether or not issues should be included in the
        ///   training set.  If included, the <see cref="IssueFilter" /> will be applied
        ///   to each issue for individual consideration.
        /// </summary>
        ///
        /// <value><c>true</c> to include issues; otherwise, <c>false</c>.</value>
        ///
        public bool IncludeIssues { get; init; }

        /// <summary>
        ///   Indicates whether or not pull requests should be included in the
        ///   training set.  If included, the <see cref="PullRequestFilter" /> will be applied
        ///   to each issue for individual consideration.
        /// </summary>
        ///
        /// <value><c>true</c> to include issues; otherwise, <c>false</c>.</value>
        ///
        public bool IncludePullRequests { get; init; }

        /// <summary>
        ///  Initializes a new instance of the <see cref="TrainingDataFilters"/> class.
        /// </summary>
        ///
        /// <param name="includeIssues">A flag indicating whether or not issues should be included in the training set.</param>
        /// <param name="includePullRequests">A flag indicating whether or not pull requests should be included in the training set.</param>
        /// <param name="requiredIssueLabelNames">The set of names identifying labels which all must be present on an issue for it to be included in the training set.</param>
        /// <param name="requiredPullRequestLabelNames">The set of names identifying labels which all must be present on a pull request for it to be included in the training set.</param>
        ///
        public TrainingDataFilters(
            bool includeIssues = true,
            bool includePullRequests = true,
            string[] requiredIssueLabelNames = default,
            string[] requiredPullRequestLabelNames = default)
        {
            IncludeIssues = includeIssues;
            IncludePullRequests = includePullRequests;
            RequiredIssueLabelNames = requiredIssueLabelNames;
            RequiredPullRequestLabelNames = requiredPullRequestLabelNames;
        }

        /// <summary>
        ///   A filter applied to the issues under consideration for use in the training set.  The filter
        ///   is only considered if <see cref="IncludeIssues" /> is set.
        /// </summary>
        ///
        /// <param name="issue">The issue to consider.</param>
        ///
        /// <returns><c>true</c> if the <paramref name="issue"/> should be included in the training set; otherwise, <c>false</c>.</returns>
        ///
        public virtual bool IssueFilter(Issue issue) => true;

        /// <summary>
        ///   A filter applied to the pull requests under consideration for use in the training set.  The
        ///   filter is only considered if <see cref="IncludePullRequests" /> is set.
        /// </summary>
        ///
        /// <param name="pullRequest">The pull request to consider.</param>
        ///
        /// <returns><c>true</c> if the <paramref name="pullRequest"/> should be included in the training set; otherwise, <c>false</c>.</returns>
        ///
        public virtual bool PullRequestFilter(PullRequestWithFiles pullRequest) => true;

        /// <summary>
        ///   A filter applied to the labels under consideration for use in the training set.
        /// </summary>
        ///
        /// <param name="label">The label to consider.</param>
        ///
        /// <returns><c>true</c> if the <paramref name="label"/> should be included in the training set; otherwise, <c>false</c>.</returns>
        ///
        public virtual bool LabelFilter(Label label) => true;
    }
}
