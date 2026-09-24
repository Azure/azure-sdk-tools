import { CodeDiagnostic } from "./codeDiagnostic";
import { CommentItemModel } from "./commentItemModel";
import { NavigationTreeNode } from "./navigationTreeModels";
import { StructuredToken } from "./structuredToken";

export enum CodePanelRowDatatype {
  CodeLine = "codeLine",
  Documentation = "documentation",
  Diagnostics = "diagnostics",
  CommentThread = "commentThread",
  CrossLanguage = "crossLanguage",
  Separator = "separator"
}

export class CodePanelRowData {
  type: string;
  lineNumber: number;
  rowOfTokens: StructuredToken[];
  nodeId: string;
  nodeIdHashed: string;
  crossLanguageId: string;
  rowPositionInGroup: number; // a group of consecutive rows can have the same nodeIdHashed. With this you can index specific rows within the group
  associatedRowPositionInGroup: number;
  rowClasses: Set<string>;
  indent: number;
  diffKind: string;
  toggleDocumentationClasses: string;
  toggleCommentsClasses: string;
  threadId: string;
  diagnostics: CodeDiagnostic;
  comments: CommentItemModel[];
  crossLanguageLines: Map<string, CrossLanguageRowDto>; // key: language
  showReplyTextBox: boolean;
  isResolvedCommentThread: boolean;
  commentThreadIsResolvedBy: string;
  isHiddenAPI: boolean;
  draftCommentText: string = '';
  
  constructor(
    type: string = '',
    lineNumber: number = 0,
    rowOfTokens: StructuredToken[] = [],
    nodeId: string = '',
    nodeIdHashed: string = '',
    rowPositionInGroup: number = 0,
    associatedRowPositionInGroup: number = 0,
    rowClasses: Set<string> = new Set<string>(),
    indent: number = 0,
    diffKind: string = '',
    toggleDocumentationClasses: string = '',
    toggleCommentsClasses: string = '',
    threadId: string = '',
    diagnostics: CodeDiagnostic = new CodeDiagnostic(),
    comments: CommentItemModel[] = [],
    crossLanguageLines: Map<string, CrossLanguageRowDto> = new Map<string, CrossLanguageRowDto>(),
    showReplyTextBox: boolean = false,
    isResolvedCommentThread: boolean = false,
    commentThreadIsResolvedBy: string = '',
    isHiddenAPI: boolean = false,
    crossLanguageId: string = '',
  ) {
    this.type = type;
    this.lineNumber = lineNumber;
    this.rowOfTokens = rowOfTokens;
    this.nodeId = nodeId;
    this.nodeIdHashed = nodeIdHashed;
    this.rowPositionInGroup = rowPositionInGroup;
    this.associatedRowPositionInGroup = associatedRowPositionInGroup;
    this.rowClasses = rowClasses;
    this.indent = indent;
    this.diffKind = diffKind;
    this.toggleDocumentationClasses = toggleDocumentationClasses;
    this.toggleCommentsClasses = toggleCommentsClasses;
    this.threadId = threadId;
    this.diagnostics = diagnostics;
    this.comments = comments;
    this.crossLanguageLines = crossLanguageLines;
    this.showReplyTextBox = showReplyTextBox;
    this.isResolvedCommentThread = isResolvedCommentThread;
    this.commentThreadIsResolvedBy = commentThreadIsResolvedBy;
    this.isHiddenAPI = isHiddenAPI;
    this.crossLanguageId = crossLanguageId;
  }
}

export interface CodePanelData {
  nodeMetaData: { [key: string]: CodePanelNodeMetaData };
  hasDiff: boolean;
  hasHiddenAPIThatIsDiff: boolean;
  navigationTreeNodes: NavigationTreeNode[];
}
  
export class CodePanelNodeMetaData {
  documentation: CodePanelRowData[];
  diagnostics: CodePanelRowData[];
  codeLines: CodePanelRowData[];
  commentThread: { [key: number]: CodePanelRowData[] };
  navigationTreeNode: NavigationTreeNode;
  parentNodeIdHashed: string;
  childrenNodeIdsInOrder: { [key: number]: string };
  isNodeWithDiff: boolean;
  isNodeWithDiffInDescendants: boolean;
  isNodeWithNoneDocDiffInDescendants : boolean;
  bottomTokenNodeIdHash: string;
  isProcessed: boolean;
  relatedNodeIdHash: string;
  
  constructor() {
    this.documentation = [];
    this.diagnostics = [];
    this.codeLines = [];
    this.commentThread = {};
    this.navigationTreeNode = new NavigationTreeNode();
    this.parentNodeIdHashed = '';
    this.childrenNodeIdsInOrder = {};
    this.isNodeWithDiff = false;
    this.isNodeWithDiffInDescendants = false;
    this.isNodeWithNoneDocDiffInDescendants = false;
    this.bottomTokenNodeIdHash = '';
    this.isProcessed = false;
    this.relatedNodeIdHash = '';
  }
}

export interface CrossLanguageRowDto {
  codeLines: CodePanelRowData[]
  apiRevisionId: string;
  reviewId: string;
  language: string;
  packageVersion: string;
  packageName: string;
}

export interface CrossLanguageContentDto {
  // key: crossLanguageId
  // value: CodePanelRowData
  content: { [key: string]: CodePanelRowData[] };
  apiRevisionId: string;
  reviewId: string;
  language: string;
  packageVersion: string;
  packageName: string;
}
