export interface ReviewQualityScore {
  score: number;
  unresolvedMustFixCount: number;
  unresolvedMustFixExcludingDiagnosticsCount: number;
  unresolvedShouldFixCount: number;
  unresolvedSuggestionCount: number;
  unresolvedQuestionCount: number;
  unresolvedUnknownCount: number;
  totalUnresolvedCount: number;
}
