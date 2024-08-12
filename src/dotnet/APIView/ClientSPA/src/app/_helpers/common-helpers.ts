export const REVIEW_ID_ROUTE_PARAM = "reviewId";
export const ACTIVE_API_REVISION_ID_QUERY_PARAM = "activeApiRevisionId";
export const DIFF_API_REVISION_ID_QUERY_PARAM = "diffApiRevisionId";
export const DIFF_STYLE_QUERY_PARAM = "diffStyle";
export const SCROLL_TO_NODE_QUERY_PARAM = "nId";
export const FULL_DIFF_STYLE = "full";
export const TREE_DIFF_STYLE = "trees";
export const NODE_DIFF_STYLE = "nodes";

export function getLanguageCssSafeName(language: string): string {
  switch (language.toLowerCase()) {
    case "c#":
      return "csharp";
    case "c++":
      return "cplusplus";
    default:
      return language.toLowerCase();
  }   
}

export function mapLanguageAliases(languages: Iterable<string>): string[] {
  const result: Set<string> = new Set<string>();
  
  for (const language of languages) {
    if (language === "TypeSpec" || language === "Cadl") {
      result.add("Cadl");
      result.add("TypeSpec");
    }
    result.add(language);
  }
  return Array.from(result);
}