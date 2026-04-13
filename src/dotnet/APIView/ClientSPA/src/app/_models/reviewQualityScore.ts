export interface ReviewQualityScore {
  score: number;
  unresolvedMustFixCount: number;
  unresolvedMustFixDiagnostics?: number;
  unresolvedShouldFixCount: number;
  unresolvedSuggestionCount: number;
  unresolvedQuestionCount: number;
  unresolvedUnknownCount: number;
  totalUnresolvedCount: number;
}
