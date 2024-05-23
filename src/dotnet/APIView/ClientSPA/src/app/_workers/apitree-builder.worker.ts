/// <reference lib="webworker" />

import { ComputeTokenDiff } from "../_helpers/worker-helpers";
import { CodeDiagnostic, CodePanelData, CommentItemModel } from "../_models/review";
import { CodePanelRowData, CodePanelRowDatatype, DiffLineInProcess, InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective, StructuredToken } from "../_models/revision";
import { APITreeNode, ApiTreeBuilderData } from "../_models/revision";

let insertLineNumber = 0;
let diffLineNumber = 0;
let diagnostics: CodeDiagnostic[] = [];
let comments: CommentItemModel[] = [];
let diagnosticsTargetIds = new Set<string>();
let apiTreeBuilderData : ApiTreeBuilderData | undefined = undefined;
let toggleDocumentationClassPart = "bi-arrow-up-square";

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    let reviewContent: CodePanelData = JSON.parse(jsonString);
    diagnostics = reviewContent.diagnostics;
    comments = reviewContent.comments;
    diagnosticsTargetIds = new Set<string>(diagnostics.map(diagnostic => diagnostic.targetId));

    insertLineNumber = 0;
    diffLineNumber = 0;

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

    diagnostics = [];
    comments = [];
    diagnosticsTargetIds.clear();
    apiTreeBuilderData = undefined;
    postMessage(updateCodeLineDataMessage);
  } else {
    apiTreeBuilderData = data;
    if (apiTreeBuilderData?.showDocumentation) {
      toggleDocumentationClassPart = "bi-arrow-down-square";
    }
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
  let navIcon = apiTreeNode.kind.toLocaleLowerCase() + ".png";
  if ("subKind" in apiTreeNode.properties) {
    navIcon = apiTreeNode.properties["subKind"].toLocaleLowerCase() + ".png";
  }

  if ("iconName" in apiTreeNode.properties) {
    navIcon = apiTreeNode.properties["iconName"].toLocaleLowerCase() + ".svg";
  }

  let treeNode: any = {
    label: apiTreeNode.name,
    data: {
      kind: (apiTreeNode.properties["subKind"]) ? apiTreeNode.properties["subKind"] : apiTreeNode.kind.toLocaleLowerCase(),
      icon: navIcon,
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
  const lineGroupOrder = ["documentation"]; // Tells you the order in which to build lines
  const nodeId = getTokenNodeIdHash(apiTreeNode, position);
  let beforeTokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  let afterTokens = (position === "top") ? apiTreeNode.topDiffTokens : apiTreeNode.bottomDiffTokens;

  let precedingRowData : CodePanelRowData | undefined = undefined;

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

    const beforeTokenIdsInLine = new Set<string>();
    const afterTokenIdsInLine = new Set<string>();

    let beforeLineGroupId : string | undefined = undefined;
    let afterLineGroupId : string | undefined = undefined;

    while (beforeIndex < beforeTokens.length) {
      const token = beforeTokens[beforeIndex++];     
      if (token.kind === "LineBreak") {
        break;
      }

      if ("groupId" in token.properties) {
        beforeLineGroupId = token.properties["groupId"];
      }
      else {
        beforeLineGroupId = undefined;
      }

      beforeTokenLine.push(token);
      if (token.id) {
        beforeTokenIdsInLine.add(token.id);
      }
    }

    if (beforeTokenLine.length > 0 ) {
      beforeTokenLines.push({
        groupId: beforeLineGroupId,
        lineTokens: beforeTokenLine,
        tokenIdsInLine: new Set(beforeTokenIdsInLine)
      });
      beforeTokenIdsInLine.clear();
    }


    while (afterIndex < afterTokens.length) {
      const token = afterTokens[afterIndex++];
      if (token.kind === "LineBreak") {
        break;
      }

      if ("groupId" in token.properties) {
        afterLineGroupId = token.properties["groupId"];
      }
      else {
        afterLineGroupId = undefined;
      }

      afterTokenLine.push(token);
      if (token.id) {
        afterTokenIdsInLine.add(token.id);
      }
    }

    if (afterTokenLine.length > 0) {
      afterTokenLines.push({
        groupId: afterLineGroupId,
        lineTokens: afterTokenLine,
        tokenIdsInLine: new Set(afterTokenIdsInLine)
      });
      afterTokenIdsInLine.clear();
    }


    if (beforeTokenLines.length > 0 || afterTokenLines.length > 0) {
      let beforeDiffTokens : Array<StructuredToken> = [];
      let afterDiffTokens : Array<StructuredToken> = [];

      let beforeTokenIdsInLine = new Set<string>();
      let afterTokenIdsInLine = new Set<string>();

      if (beforeTokenLines.length > 0 && afterTokenLines.length > 0) {
        if (beforeTokenLines[0].groupId === afterTokenLines[0].groupId) {
          (beforeTokenLines[0].groupId) ? beforeLineClasses.add(beforeTokenLines[0].groupId) : null;
          (afterTokenLines[0].groupId!) ? afterLineClasses.add(afterTokenLines[0].groupId!) : null;
          beforeTokenIdsInLine = beforeTokenLines[0].tokenIdsInLine;
          afterTokenIdsInLine = afterTokenLines[0].tokenIdsInLine;
          beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
          afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
        }
        else {
          const beforeTokenLineBuildOrder = lineGroupOrder.indexOf(beforeTokenLines[0].groupId!);
          const afterTokenLineBuildOrder = lineGroupOrder.indexOf(afterTokenLines[0].groupId!);
          if ((afterTokenLineBuildOrder < 0) || (beforeTokenLineBuildOrder >= 0 && beforeTokenLineBuildOrder < afterTokenLineBuildOrder)) {
            (beforeTokenLines[0].groupId) ? beforeLineClasses.add(beforeTokenLines[0].groupId) : null;
            beforeTokenIdsInLine = beforeTokenLines[0].tokenIdsInLine;
            beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
          }
          else {
            (afterTokenLines[0].groupId!) ? afterLineClasses.add(afterTokenLines[0].groupId!) : null;
            afterTokenIdsInLine = afterTokenLines[0].tokenIdsInLine;
            afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
          }
        }
      }
      else if(beforeTokenLines.length > 0) {
        (beforeTokenLines[0].groupId) ? beforeLineClasses.add(beforeTokenLines[0].groupId) : null;
        beforeTokenIdsInLine = beforeTokenLines[0].tokenIdsInLine;
        beforeDiffTokens = beforeTokenLines.shift()?.lineTokens!;
      }
      else {
        (afterTokenLines[0].groupId!) ? afterLineClasses.add(afterTokenLines[0].groupId!) : null;
        afterTokenIdsInLine = afterTokenLines[0].tokenIdsInLine;
        afterDiffTokens = afterTokenLines.shift()?.lineTokens!;
      }

      const diffTokenLineResult = ComputeTokenDiff(beforeDiffTokens, afterDiffTokens) as [StructuredToken[], StructuredToken[], boolean];

      let insertLineOfTokensMessage : InsertCodePanelRowDataMessage =  {
        directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.CodeLine,
          lineNumber: 0,
          lineTokens : diffTokenLineResult[0],
          rowClasses : new Set(beforeLineClasses), //new set to avoid reference sharing
          nodeId : nodeId,
          nodeIdUnHashed: id,
          tokenPosition: position,
          indent : indent,
          diffKind : "Unchanged",
          rowSize : 21
        } 
      };

      if (diffTokenLineResult[2] === true) {
        if (diffTokenLineResult[0].length > 0) {
          insertLineOfTokensMessage.codePanelRowData.diffKind = "Removed";
          beforeLineClasses.add("removed");
          insertLineOfTokensMessage.codePanelRowData.rowClasses = new Set(beforeLineClasses);
          
          insertLineNumber++;
          insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
          lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show` :
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;

          // Collects comments for the line
          let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(new Set<string>, id, position, nodeId, insertLineOfTokensMessage);

          precedingRowData = insertLineOfTokensMessage.codePanelRowData;

          if (!(apiTreeBuilderData?.onlyDiff && apiTreeNode.children.length === 0 && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Added" && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Removed")) {
            postMessage(insertLineOfTokensMessage);
          }
          
          beforeLineClasses.clear();
        }

        if (diffTokenLineResult[1].length > 0) {
          insertLineOfTokensMessage.codePanelRowData.lineTokens = diffTokenLineResult[1];
          insertLineOfTokensMessage.codePanelRowData.diffKind = "Added";
          afterLineClasses.add("added");
          insertLineOfTokensMessage.codePanelRowData.rowClasses = new Set(afterLineClasses);

          insertLineNumber++;
          diffLineNumber++;
          insertLineOfTokensMessage.codePanelRowData.lineNumber = diffLineNumber;
          lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show` :
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;

          // Collects comments for the line
          let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(afterTokenIdsInLine, id, position, nodeId, insertLineOfTokensMessage);
          
          precedingRowData = insertLineOfTokensMessage.codePanelRowData;
          if (!(apiTreeBuilderData?.onlyDiff && apiTreeNode.children.length === 0 && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Added" && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Removed")) {
            postMessage(insertLineOfTokensMessage);
          }
          afterLineClasses.clear();

          if (insertCommentMessage) {
            if (!(apiTreeBuilderData?.onlyDiff && apiTreeNode.children.length === 0 && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Added" && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Removed")) {
              postMessage(insertLineOfTokensMessage);
            }
          }
        }
      }
      else {
        if (diffTokenLineResult[0].length > 0) {
          insertLineNumber++;
          diffLineNumber++;
          insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
          lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show` :
            insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;

          // Collects comments for the line
          let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(afterTokenIdsInLine, id, position, nodeId, insertLineOfTokensMessage);

          precedingRowData = insertLineOfTokensMessage.codePanelRowData;
          if (!(apiTreeBuilderData?.onlyDiff && apiTreeNode.children.length === 0 && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Added" && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Removed")) {
            postMessage(insertLineOfTokensMessage);
          }
          beforeLineClasses.clear();

          if (insertCommentMessage) {
            if (!(apiTreeBuilderData?.onlyDiff && apiTreeNode.children.length === 0 && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Added" && insertLineOfTokensMessage.codePanelRowData.diffKind !== "Removed")) {
              postMessage(insertLineOfTokensMessage);
            }
          }
        }
      }
    }
  }

  // Append associated diagnostics rows
  addDiagnosticRow(id, position, nodeId);
}

/**
 * Build the tokens for if the node contains no diff
 * @param apiTreeNode 
 * @param id 
 * @param position
 * @returns noOfLinesInserted
 */
function buildTokensForNonDiffNodes(apiTreeNode: APITreeNode, id: string, position: string, indent: number = 0) {
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
          nodeIdUnHashed: id,
          tokenPosition: position,
          rowClasses : new Set(lineClasses), //new set to avoid reference sharing
          indent : indent,
          diffKind : "NoneDiff",
          rowSize : 21
        }
      };

      insertLineNumber++;
      diffLineNumber++;
      insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
      lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show` :
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;
      
      // Collects comments for the line
      let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(tokenIdsInLine, id, position, nodeId, insertLineOfTokensMessage);

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
        nodeIdUnHashed: id,
        tokenPosition: position,
        rowClasses : new Set(lineClasses), //new set to avoid reference sharing
        indent : indent,
        diffKind : "NoneDiff",
        rowSize : 21
      } 
    };

    insertLineNumber++;
    diffLineNumber++;
    insertLineOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
    lineHasDocumentationAbove(precedingRowData, insertLineOfTokensMessage.codePanelRowData) ?
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show` :
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;

    // Collects comments for the line  
    let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = collectUserCommentsforLine(tokenIdsInLine, id, position, nodeId, insertLineOfTokensMessage);

    postMessage(insertLineOfTokensMessage);

    // Push comments after pussing the line
    if (insertCommentMessage) {
      postMessage(insertCommentMessage);
    }
  }

  // Append associated diagnostics rows
  addDiagnosticRow(id, position, nodeId);
}

function lineHasDocumentationAbove(precedingLine : CodePanelRowData | undefined, currentLine : CodePanelRowData) : boolean {
  return (precedingLine !== undefined && precedingLine.rowClasses?.has("documentation") && !currentLine.rowClasses?.has("documentation")) ?? false;
}

function collectUserCommentsforLine(tokenIdsInLine: Set<string>, id: string, position: string, nodeId: string, insertLineOfTokensMessage : InsertCodePanelRowDataMessage) : InsertCodePanelRowDataMessage | undefined {
  let insertCommentMessage : InsertCodePanelRowDataMessage | undefined = undefined;
  let toggleCommentClass = (diagnosticsTargetIds.has(id)) ? "bi bi-chat-right-text show" : "";
  if (tokenIdsInLine.size > 0) {
    toggleCommentClass = (!toggleCommentClass) ? "bi bi-chat-right-text can-show" : toggleCommentClass;
    insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = toggleCommentClass;
    const commentsForLine = comments.filter(comment => tokenIdsInLine.has(comment.elementId));

    if (commentsForLine.length > 0) {
      insertCommentMessage = {
        directive: ReviewPageWorkerMessageDirective.InsertCommentRowData,
        codePanelRowData: {
          rowType: CodePanelRowDatatype.CommentThread,
          nodeId: nodeId,
          nodeIdUnHashed: id,
          tokenPosition: position,
          rowClasses: new Set<string>(["user-comment-thread"]),
          comments: commentsForLine,
          rowSize: 21
        }
      };
      toggleCommentClass = toggleCommentClass.replace("can-show", "show");
      insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = toggleCommentClass;
    }
  }
  else {
    toggleCommentClass = (toggleCommentClass) ? toggleCommentClass.replace("show", "hide") : "bi bi-chat-right-text hide";
    insertLineOfTokensMessage.codePanelRowData.toggleCommentsClasses = toggleCommentClass;
  }
  return insertCommentMessage;
}

function addDiagnosticRow(id: string, position: string, nodeId: string){
  if (diagnosticsTargetIds.has(id) && position !== "bottom") {
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
          nodeIdUnHashed: id,
          tokenPosition: position,
          rowClasses: new Set<string>(rowClasses),
          rowSize: rowSize,
          diagnostics: diagnostisRow
        }
      };
      postMessage(insertDiagnosticMessage);
    });
  }
}
