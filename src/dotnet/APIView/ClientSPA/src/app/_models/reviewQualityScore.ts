export interface ReviewQualityScore {
  score: number;
  unresolvedMustFixCount: number;
  unresolvedShouldFixCount: number;
  unresolvedSuggestionCount: number;
  unresolvedQuestionCount: number;
  unresolvedUnknownCount: number;
  totalUnresolvedCount: number;
}
