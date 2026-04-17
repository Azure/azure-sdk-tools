import { AIReviewJobCompletedDto } from "../_dtos/aiReviewJobCompletedDto";
import { CodePanelRowData, CodePanelRowDatatype } from "../_models/codePanelModels";
import { SiteNotification } from "../_models/notificationsModel";
import { StructuredToken } from "../_models/structuredToken";
import { ToastMessageData } from "../_models/toastMessageModel";

export const FULL_DIFF_STYLE = "full";
export const TREE_DIFF_STYLE = "trees";
export const NODE_DIFF_STYLE = "nodes";
export const MANUAL_ICON = "fa-solid fa-arrow-up-from-bracket";
export const PR_ICON = "fa-solid fa-code-pull-request";
export const AUTOMATIC_ICON = "fa-solid fa-robot";
export const DIFF_ADDED = "added";
export const DIFF_REMOVED = "removed";
export const INDEXED_DB_NAME = "apiview-indexed-db";

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

export function convertRowOfTokensToString(rowOfTokens: StructuredToken[]): string {
  return rowOfTokens.map(token => token.value).join('');
}

export function getSupportedLanguages(): any {
  return [
    { label: "C", data: "C" },
    { label: "C#", data: "C#" },
    { label: "C++", data: "C++" },
    { label: "Go", data: "Go" },
    { label: "Java", data: "Java" },
    { label: "JavaScript", data: "JavaScript" },
    { label: "Json", data: "Json" },
    { label: "Kotlin", data: "Kotlin" },
    { label: "Python", data: "Python" },
    { label: "Rust", data: "Rust" },
    { label: "Swagger", data: "Swagger" },
    { label: "Swift", data: "Swift" },
    { label: "TypeSpec", data: "TypeSpec" },
    { label: "Xml", data: "Xml" }
  ];
}

export function getAIReviewNotificationInfo(jobInfo : AIReviewJobCompletedDto, origin: string): [SiteNotification, any] | undefined {
  if (jobInfo.status == 'Success' && jobInfo.noOfGeneratedComments > 0) {
    const messageData : ToastMessageData = {
      action: 'RefreshPage',
    };
    const pageUrl = `${origin}/review/${jobInfo.reviewId}?activeApiRevisionId=${jobInfo.apirevisionId}`;
    const messagePart = (jobInfo.noOfGeneratedComments === 1) ? "comment" : "comments";
    const messageDetail = `Copilot generated ${jobInfo.noOfGeneratedComments} ${messagePart}.`;
    const summary = 'Copilot Comments';
    const severity = 'success';
    const toastNotification = { severity: severity, icon: 'bi bi-check-circle', summary: summary, detail: messageDetail, data: messageData, key: 'bc', life: 60000, closable: true };
    const notification = new SiteNotification(
      jobInfo.reviewId,
      jobInfo.apirevisionId,
      summary,
      messageDetail + `</br>Job Id: ${jobInfo.jobId}</br><a href="${pageUrl}" target="_blank">View Review</a>`,
      severity
    );
    return [notification, toastNotification];
  } else if (jobInfo.status == 'Error') {
    const message = 'Failed to generate copilot review';
    const summary = 'AI Comments';
    const severity = 'error';
    const toastNotification = { severity: severity, icon: 'bi bi-exclamation-triangle', summary: summary, detail: message, key: 'bc', life: 5000, closable: true };
    const notification = new SiteNotification(
      jobInfo.reviewId,
      jobInfo.apirevisionId,
      summary,
      message + ` Job Id: ${jobInfo.jobId}, ${jobInfo.details}`,
      severity
    );
    return [notification, toastNotification];
  }
  return undefined;
}

export function getCodePanelRowDataClass(row: CodePanelRowData) {
  let classObject: { [key: string]: boolean } = {};
  if (row.rowClasses) {
    for (let className of Array.from(row.rowClasses)) {
      classObject[className] = true;
    }
  }

  if (row.isHiddenAPI) {
    classObject['hidden-api'] = true;
  }
  return classObject;
}

export function getStructuredTokenClass(token: StructuredToken) {
  let classObject: { [key: string]: boolean } = {};
  if (token.renderClasses) {
    for (let className of Array.from(token.renderClasses)) {
      classObject[className] = true;
    }
  }

  if (token.properties && 'NavigateToUrl' in token.properties) {
    classObject['url-token'] = true;
  }

  if (token.properties && 'NavigateToId' in token.properties) {
    classObject['nav-token'] = true;
  }

  if (token.tags && new Set(token.tags).has('Deprecated')) {
    classObject['deprecated-token'] = true;
  }
  return classObject;
}
