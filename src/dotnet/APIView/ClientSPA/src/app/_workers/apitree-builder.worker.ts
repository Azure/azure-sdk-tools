/// <reference lib="webworker" />

import { ComputeTokenDiff } from "../_helpers/worker-helpers";
import { CodeDiagnostic, CodePanelData } from "../_models/review";
import { CodePanelRowData, CodePanelRowDatatype, InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective, StructuredToken } from "../_models/revision";
import { APITreeNode } from "../_models/revision";

let insertLineNumber = 0;
let diagnostics: CodeDiagnostic[] = [];
let diagnosticsTargetIds = new Set<string>();

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    let reviewContent: CodePanelData = JSON.parse(jsonString);
    diagnostics = reviewContent.diagnostics;
    diagnosticsTargetIds = new Set<string>(diagnostics.map(diagnostic => diagnostic.targetId));

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
    },
    expanded: true,
    children: []
  }

  let children : any[] = [];
  apiTreeNode.children.forEach(child => {
    const childTreeNodes = buildAPITree(child, treeNodeId, indent + 1);
    if (child.children.length > 0) {
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
  const subKind = apiTreeNode.properties["SubKind"];
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
  const beforeTokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  const afterTokens = (position === "top") ? apiTreeNode.topDiffTokens : apiTreeNode.bottomDiffTokens;

  let beforeIndex = 0;
  let afterIndex = 0;

  while (beforeIndex < beforeTokens.length || afterIndex < afterTokens.length) {
    const beforeTokenLine : Array<StructuredToken> = [];
    const afterTokenLine : Array<StructuredToken> = [];

    const beforeLineClasses = new Set<string>();
    const afterLineClasses = new Set<string>();

    while (beforeIndex < beforeTokens.length) {
      const token = beforeTokens[beforeIndex++];     
      if (token.kind === "LineBreak") {
        break;
      }

      if ("GroupId" in token.properties) {
        beforeLineClasses.add(token.properties["GroupId"]);
      }
      beforeTokenLine.push(token);
    }

    while (afterIndex < afterTokens.length) {
      const token = afterTokens[afterIndex++];
      if (token.kind === "LineBreak") {
        break;
      }

      if ("GroupId" in token.properties) {
        afterLineClasses.add(token.properties["GroupId"]);
      }
      afterTokenLine.push(token);
    }

    const diffTokenLineResult = ComputeTokenDiff(beforeTokenLine, afterTokenLine) as [StructuredToken[], StructuredToken[], boolean];

    let insertLinesOfTokensMessage : InsertCodePanelRowDataMessage =  {
      directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
      codePanelRowData: {
        rowType: CodePanelRowDatatype.CodeLine,
        lineNumber: 0,
        lineTokens : diffTokenLineResult[0],
        rowClasses : beforeLineClasses,
        nodeId : id,
        indent : indent,
        diffKind : "Unchanged",
        rowSize : 21
      } 
    };

    if (diffTokenLineResult[2] === true) {
      insertLinesOfTokensMessage.codePanelRowData.diffKind = "Removed";
      insertLinesOfTokensMessage.codePanelRowData.rowClasses = beforeLineClasses;
      
      insertLineNumber++;
      insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
      postMessage(insertLinesOfTokensMessage);

      insertLinesOfTokensMessage.codePanelRowData.lineTokens = diffTokenLineResult[1];
      insertLinesOfTokensMessage.codePanelRowData.diffKind = "Added";
      insertLinesOfTokensMessage.codePanelRowData.rowClasses = afterLineClasses;

      insertLineNumber++;
      insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
      postMessage(insertLinesOfTokensMessage);
    }
    else {
      insertLineNumber++;
      insertLinesOfTokensMessage.codePanelRowData.lineNumber = insertLineNumber;
      postMessage(insertLinesOfTokensMessage);
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
  let precedingRowData : CodePanelRowData | undefined = undefined;

  for (let token of tokens) {
    if ("GroupId" in token.properties) {
      lineClasses.add(token.properties["GroupId"]);
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
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square show" :
        insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square hide";

      precedingRowData = insertLineOfTokensMessage.codePanelRowData;
      postMessage(insertLineOfTokensMessage);

      tokenLine.length = 0;
      lineClasses.clear();
    }
    else {
      tokenLine.push(token);
    }
  }

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
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square show" :
      insertLineOfTokensMessage.codePanelRowData.toggleDocumentationClasses = "bi bi-arrow-up-square hide";

    postMessage(insertLineOfTokensMessage);
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
