namespace APIViewWeb.LeanModels
{
    /// <summary>
    /// Represents the computed quality score for an API review based on unresolved comments.
    /// Score starts at 100 and is degraded by unresolved comments weighted by severity.
    /// AI-generated comment penalties are scaled by their individual confidence scores.
    /// </summary>
    public class ReviewQualityScore
    {
        /// <summary>
        /// Overall quality score from 0 to 100. Higher is better.
        /// </summary>
        public double Score { get; set; } = 100;

        /// <summary>
        /// Number of unresolved MustFix comments.
        /// </summary>
        public int UnresolvedMustFixCount { get; set; }

        /// <summary>
        /// Number of unresolved ShouldFix comments.
        /// </summary>
        public int UnresolvedShouldFixCount { get; set; }

        /// <summary>
        /// Number of unresolved Suggestion comments.
        /// </summary>
        public int UnresolvedSuggestionCount { get; set; }

        /// <summary>
        /// Number of unresolved Question comments (do not affect score).
        /// </summary>
        public int UnresolvedQuestionCount { get; set; }

        /// <summary>
        /// Number of unresolved comments with no severity set (Unknown).
        /// These receive a ShouldFix-equivalent penalty to incentivize setting severity.
        /// </summary>
        public int UnresolvedUnknownCount { get; set; }

        /// <summary>
        /// Total number of unresolved comments considered in the score calculation.
        /// </summary>
        public int TotalUnresolvedCount { get; set; }

        // Severity penalty weights (points deducted per unresolved comment)
        internal const double MustFixPenalty = 20.0;
        internal const double ShouldFixPenalty = 10.0;
        internal const double UnknownPenalty = 10.0;
    }
}
