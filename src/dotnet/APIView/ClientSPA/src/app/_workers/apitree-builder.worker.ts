/// <reference lib="webworker" />

import { ComputeTokenDiff } from "../_helpers/worker-helpers";
import { CodeDiagnostic, CodePanelData, CommentItemModel } from "../_models/review";
import { CodePanelRowData, CodePanelRowDatatype, DiffLineInProcess, InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective, StructuredToken } from "../_models/revision";
import { APITreeNode } from "../_models/revision";

let insertLineNumber = 0;
let diagnostics: CodeDiagnostic[] = [];
let comments: CommentItemModel[] = [];
let diagnosticsTargetIds = new Set<string>();

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    let reviewContent: CodePanelData = JSON.parse(jsonString);
    diagnostics = reviewContent.diagnostics;
    comments = reviewContent.comments;
    diagnosticsTargetIds = new Set<string>(diagnostics.map(diagnostic => diagnostic.targetId));

    insertLineNumber = 0;

    let navTreeNodes: any[] = [];
    let treeNodeId : string[] = [];
  
    reviewContent.apiForest.forEach((apiTreeNode: APITreeNode) => {
      navTreeNodes.push(buildAPITree(apiTreeNode as APITreeNode, treeNodeId));
    });
  
    const createNavigationMessage =  {
      directive: ReviewPageWorkerMessageDirective.CreatePageNavigation,
      navTree : navTreeNodes
    };

    postMessage(createNavigationMessage);

    const updateCodeLineDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodeLines
    };

    postMessage(updateCodeLineDataMessage);
    diagnostics = [];
    comments = [];
    diagnosticsTargetIds.clear();
  }
});


/**
 * Walks the API tree depth first. At each node it
 * - Sends a message to the code-panel component to create a husk for the node content.
 * - Sends a message t0 the token-builder worker to build the top tokens for the node content.
 * - Recursively builds the tree for the children of the node.
 * - Sends a message to the code-panel component to create a husk for the bottom tokens of the node content.
 * - Sends a message t0 the token-builder worker to build the bottom tokens for the node content.
 * @param apiTreeNode The current node in the API tree.
 * @param treeNodeId The id of the current node in the tree.
 * @param indent The indent level of the current node in the tree.
 */
function buildAPITree(apiTreeNode: APITreeNode, treeNodeId : string[], indent: number = 0) : any {
  buildTokens(apiTreeNode, apiTreeNode.id, "top", indent);

  let treeNode: any = {
    label: apiTreeNode.name,
    data: {
      nodeIndex: insertLineNumber,
      kind: (apiTreeNode.properties["subKind"]) ? apiTreeNode.properties["subKind"] : apiTreeNode.kind.toLocaleLowerCase()
    },
    expanded: true,
    children: []
  }

  let children : any[] = [];
  apiTreeNode.children.forEach((child : APITreeNode) => {
    const childTreeNodes = buildAPITree(child, treeNodeId, indent + 1);
    if (!Array.from(child.tags).includes("HideFromNavigation")) {
      children.push(childTreeNodes);
    }
  });

  if (apiTreeNode.bottomTokens.length > 0) {
    buildTokens(apiTreeNode, apiTreeNode.id, "bottom", indent);
  }

  treeNode.children = children;
  treeNodeId.pop();
  return treeNode;
}

/**
 * Uses the properties of the node to create an Id that is guaranteed to be unique
 * @param apiTreeNode 
 */
function getTokenNodeIdHash(apiTreeNode: APITreeNode, position: string) {
  const kind = apiTreeNode.kind;
  const subKind = apiTreeNode.properties["subKind"];
  const id = apiTreeNode.id;

  let idPart = kind;
  if (subKind) {
    idPart = `${idPart}-${subKind}`;
  }

  if (id) {
    idPart = `${idPart}-${id}`;
  }
  idPart = `${idPart}-${position}`;
  return createHashFromString(idPart);
}

/**
 * Creates Unique Hash from a string
 * @param inputString 
 */
function createHashFromString(inputString: string) {
  const hash = inputString.split('').reduce((prevHash, currVal) =>
    ((prevHash << 5) - prevHash) + currVal.charCodeAt(0), 0);

  const cssId = 'id' + hash.toString();

  return cssId;
}

function buildTokens(apiTreeNode: APITreeNode, id: string, position: string, indent: number = 0) {
  if (apiTreeNode.diffKind === "NoneDiff") {
    buildTokensForNonDiffNodes(apiTreeNode, id, position, indent);
  }
  else {
    buildTokensForDiffNodes(apiTreeNode, id, position, indent);
  }
}

/**
 * Build the tokens for if the node potentially contains diff
 * @param apiTreeNode 
 * @param id 
 * @param position 
 */
function buildTokensForDiffNodes(apiTreeNode: APITreeNode, id: string, position: string, indent: number = 0) {
  const lineGroupOrder = ["documentation"];
  const nodeId = getTokenNodeIdHash(apiTreeNode, position);
  let beforeTokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  let afterTokens = (position === "top") ? apiTreeNode.topDiffTokens : apiTreeNode.bottomDiffTokens;

  if (apiTreeNode.diffKind === "Added") {
    afterTokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
    beforeTokens = [];
  }
  
  const beforeTokenLines  : Array<DiffLineInProcess> = [];
  const afterTokenLines  : Array<DiffLineInProcess> = [];

  let beforeIndex = 0;
  let afterIndex = 0;

  while (beforeIndex < beforeTokens.length || afterIndex < afterTokens.length || beforeTokenLines.length > 0 || afterTokenLines.length > 0) {
    const beforeTokenLine : Array<StructuredToken> = [];
    const afterTokenLine : Array<StructuredToken> = [];

    const beforeLineClasses = new Set<string>();
    const afterLineClasses = new Set<string>();

    let beforeLineGroupId : string | undefined = undefined;
    let afterLineGroupId : string | undefined = undefined;

    while (beforeIndex < beforeTokens.length) {
      const token = beforeTokens[beforeIndex++];     
      if (token.kind === "LineBreak") {
        break;
      }

      if ("groupId" in token.properties) {
        beforeLineGroupId = token.properties["groupId"];
        beforeLineClasses.add(beforeLineGroupId);
      }
      else {
        beforeLineGroupId = undefined;
      }

      beforeTokenLine.push(token);
    }

    if (beforeTokenLine.length > 0 ) {
      beforeTokenLines.push({
        groupId: beforeLineGroupId,
        lineTokens: beforeTokenLine
      })
    }


    while (afterIndex < afterTokens.length) {
      const token = afterTokens[afterIndex++];
      if (token.kind === "LineBreak") {
        break;
      }

      if ("groupId" in token.properties) {
        afterLineGroupId = token.properties["groupId"];
        afterLineClasses.add(afterLineGroupId);
      }
      else {
        afterLineGroupId = undefined;
      }

      afterTokenLine.push(token);
    }

    if (afterTokenLine.length > 0) {
      afterTokenLines.push({
        groupId: afterLineGroupId,
        lineTokens: afterTokenLine
      })
    }

    if (beforeTokenLines.length > 0 || afterTokenLines.length > 0) {
      let beforeDiffTokens : Array<StructuredToken> = [];
      let afterDiffTokens : Array<StructuredToken> = [];

      if (beforeTokenLines.length > 0 && afterTokenLines.length > 0) {
        if (beforeTokenLines[0].groupId === afterTokenLines[0].groupId) {
          beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
          afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
        }
        else {
          const beforeTokenLineBuildOrder = lineGroupOrder.indexOf(beforeTokenLines[0].groupId!);
          const afterTokenLineBuildOrder = lineGroupOrder.indexOf(afterTokenLines[0].groupId!);
          if ((afterTokenLineBuildOrder < 0) || (beforeTokenLineBuildOrder >= 0 && beforeTokenLineBuildOrder < afterTokenLineBuildOrder)) {
            beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
          }
          else {
            afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
          }
        }
      }
      else if(beforeTokenLines.length > 0) {
        beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
      }
      else {
        afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
      }

      const diffTokenLineResult = ComputeTokenDiff(beforeDiffTokens, afterDiffTokens) as [StructuredToken[], StructuredToken[], boolean];

      let insertLinesOfTokensMessage : InsertCodePanelRowDataMessage =  {
        directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.CodeLine,
          lineNumber: 0,
          lineTokens : diffTokenLineResult[0],
          rowClasses : beforeLineClasses,
          nodeId : nodeId,
          indent : indent,
          diffKind : "Unchanged",
          rowSize : 21
        } 
      };

      if (diffTokenLineResult[2] === true) {
        if (diffTokenLineResult[0].length > 0) {
          insertLinesOfTokensMessage.codePanelRowData.diffKind = "Removed";
          beforeLineClasses.add("removed");
          insertLinesOfTokensMessage.codePanelRowData.rowClasses = beforeLineClasses;
          
          insertLineNumber++;
          insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
          postMessage(insertLinesOfTokensMessage);
          beforeLineClasses.clear();
        }

        if (diffTokenLineResult[1].length > 0) {
          insertLinesOfTokensMessage.codePanelRowData.lineTokens = diffTokenLineResult[1];
          insertLinesOfTokensMessage.codePanelRowData.diffKind = "Added";
          afterLineClasses.add("added");
          insertLinesOfTokensMessage.codePanelRowData.rowClasses = afterLineClasses;

          insertLineNumber++;
          insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
          postMessage(insertLinesOfTokensMessage);
          afterLineClasses.clear();
        }
      }
      else {
        if (diffTokenLineResult[0].length > 0) {
          insertLineNumber++;
          insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
          postMessage(insertLinesOfTokensMessage);
        }
      }
    }
  }
}

/**
 * Build the tokens for if the node contains no diff
 * @param apiTreeNode 
 * @param id 
 * @param position
 * @returns noOfLinesInserted
 */
function buildTokensForNonDiffNodes(apiTreeNode: APITreeNode, id: string, position: string, indent: number = 0)
{
  const nodeId = getTokenNodeIdHash(apiTreeNode, position);
  const tokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  const tokenLine : StructuredToken[] = [];
  const lineClasses = new Set<string>();
  const tokenIdsInLine = new Set<string>();
  let precedingRowData : CodePanelRowData | undefined = undefined;


  for (let token of tokens) {
    if ("groupId" in token.properties) {
      lineClasses.add(token.properties["groupId"]);
    }
    
    if (token.kind === "LineBreak") {
      const insertLineOfTokensMessage : InsertCodePanelRowDataMessage =  {
        directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.CodeLine,
          lineNumber: 0,
          lineTokens : tokenLine,
          nodeId : nodeId,
          rowClasses : new Set(lineClasses), //new set to avoid reference sharing
          indent : indent,
          diffKind : "NoneDiff",
          rowSize : 21
        }
      };

      insertLineNumber++;
      insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
      lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square can-show" :
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square hide";
      
      // Collects comments for the line
      let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(tokenIdsInLine, nodeId, insertLineOfTokensMessage);

      precedingRowData = insertLineOfTokensMessage.codePanelRowData;
      postMessage(insertLineOfTokensMessage);

      // Push comments after pussing the line
      if (insertCommentMessage) {
        postMessage(insertCommentMessage);
      }

      tokenLine.length = 0;
      lineClasses.clear();
      tokenIdsInLine.clear();
    }
    else {
      tokenLine.push(token);
      if (token.id) {
        tokenIdsInLine.add(token.id);
      }
    }
  }

  // Handle any remaining lines
  if (tokenLine.length > 0) {
    const insertLineOfTokensMessage : InsertCodePanelRowDataMessage =  {
      directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
      codePanelRowData: {
        rowType: CodePanelRowDatatype.CodeLine,
        lineNumber: 0,
        lineTokens : tokenLine,
        nodeId : nodeId,
        rowClasses : new Set(lineClasses), //new set to avoid reference sharing
        indent : indent,
        diffKind : "NoneDiff",
        rowSize : 21
      } 
    };

    insertLineNumber++;
    insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
    lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square can-show" :
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square hide";

    // Collects comments for the line  
    let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(tokenIdsInLine, nodeId, insertLineOfTokensMessage);

    postMessage(insertLineOfTokensMessage);

    // Push comments after pussing the line
    if (insertCommentMessage) {
      postMessage(insertCommentMessage);
    }
  }

  // Append associated diagnostics rows
  if (diagnosticsTargetIds.has(id)) {
    const diagnosticsRows = diagnostics.filter(diagnostic => diagnostic.targetId === id);
    diagnosticsRows.forEach(diagnostisRow => {
      const rowSize = 21
      let rowClasses = ["diagnostics"];
      rowClasses.push(diagnostisRow.level.toLowerCase());
      
      const insertDiagnosticMessage : InsertCodePanelRowDataMessage = {
        directive: ReviewPageWorkerMessageDirective.InsertDiagnosticsRowData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.Diagnostics,
          nodeId: nodeId,
          rowClasses: new Set<string>(rowClasses),
          rowSize: rowSize,
          diagnostics: diagnostisRow
        }
      };
      
      postMessage(insertDiagnosticMessage);
    });
  }
}

function lineHasDocumentationAbove(precedingLine : CodePanelRowData | undefined, currentLine : CodePanelRowData) : boolean {
  return precedingLine !== undefined && precedingLine.rowClasses.has("documentation") && !currentLine.rowClasses.has("documentation");
}

function collectUserCommentsforLine(tokenIdsInLine: Set<string>, nodeId: string, insertLineOfTokensMessage : InsertCodePanelRowDataMessage) : InsertCodePanelRowDataMessage | undefined {
  let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = undefined;
  if (tokenIdsInLine.size > 0) {
    insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = "bi bi-chat-right-text can-show";
    const commentsForLine = comments.filter(comment => tokenIdsInLine.has(comment.elementId));

    if (commentsForLine.length > 0) {
      insertCommentMessage = {
        directive: ReviewPageWorkerMessageDirective.InsertCommentRowData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.CommentThread,
          nodeId: nodeId,
          rowClasses: new Set<string>(["user-comment-thread"]),
          comments: commentsForLine,
          rowSize: 21
        }
      };
      insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = "bi bi-chat-right-text show";
    }
  }
  else {
    insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = "bi bi-chat-right-text hide";
  }
  return insertCommentMessage;
}
