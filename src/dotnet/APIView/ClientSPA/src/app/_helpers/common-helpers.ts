import { CodePanelRowData, CodePanelRowDatatype } from "../_models/codePanelModels";

export const FULL_DIFF_STYLE = "full";
export const TREE_DIFF_STYLE = "trees";
export const NODE_DIFF_STYLE = "nodes";
export const MANUAL_ICON = "fa-solid fa-arrow-up-from-bracket";
export const PR_ICON = "fa-solid fa-code-pull-request";
export const AUTOMATIC_ICON = "fa-solid fa-robot";
export const DIFF_ADDED = "added";
export const DIFF_REMOVED = "removed";

export enum CodeLineRowNavigationDirection {
  prev = 0,
  next
}

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

export function getTypeClass(type: string): string {
  let result = "";
  switch (type) {
    case 'manual':
      result = MANUAL_ICON;
      break;
    case 'pullRequest':
      result = PR_ICON;
      break;
    case 'automatic':
      result = AUTOMATIC_ICON;
      break;
  }
  return result;
}

export function isDiffRow(row: CodePanelRowData) {
  return row.type === CodePanelRowDatatype.CodeLine && (row.diffKind === DIFF_REMOVED || row.diffKind === DIFF_ADDED);
}