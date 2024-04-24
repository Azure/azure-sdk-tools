/// <reference lib="webworker" />

import { ComputeTokenDiff } from "../_helpers/worker-helpers";
import { ReviewContent } from "../_models/review";
import { CodeLineData, InsertCodeLineDataMessage, ReviewPageWorkerMessageDirective, StructuredToken } from "../_models/revision";
import { APITreeNode } from "../_models/revision";

let insertLineNumber = 0;

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    let reviewContent: ReviewContent = JSON.parse(jsonString);

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

    const updateCodeLineData = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodeLines
    };

    postMessage(updateCodeLineData);
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
  let nodeId = getTokenNodeIdHash(apiTreeNode, "top");

  buildTokens(apiTreeNode, nodeId, "top", indent);

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
    let nodeId = getTokenNodeIdHash(apiTreeNode, "bottom");
    buildTokens(apiTreeNode, nodeId, "bottom", indent);
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

    let insertLinesOfTokensMessage : InsertCodeLineDataMessage =  {
      directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
      codeLineData: {
        lineNumber: 0,
        lineTokens : diffTokenLineResult[0],
        lineClasses : beforeLineClasses,
        nodeId : id,
        indent : indent,
        diffKind : "Unchanged"
      } 
    };

    if (diffTokenLineResult[2] === true) {
      insertLinesOfTokensMessage.codeLineData.diffKind = "Removed";
      insertLinesOfTokensMessage.codeLineData.lineClasses = beforeLineClasses;
      
      insertLineNumber++;
      insertLinesOfTokensMessage.codeLineData.lineNumber = insertLineNumber;
      postMessage(insertLinesOfTokensMessage);

      insertLinesOfTokensMessage.codeLineData.lineTokens = diffTokenLineResult[1];
      insertLinesOfTokensMessage.codeLineData.diffKind = "Added";
      insertLinesOfTokensMessage.codeLineData.lineClasses = afterLineClasses;

      insertLineNumber++;
      insertLinesOfTokensMessage.codeLineData.lineNumber = insertLineNumber;
      postMessage(insertLinesOfTokensMessage);
    }
    else {

      insertLineNumber++;
      insertLinesOfTokensMessage.codeLineData.lineNumber = insertLineNumber;
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
  const tokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  const tokenLine : StructuredToken[] = [];
  const lineClasses = new Set<string>();

  for (let token of tokens) {
    if ("GroupId" in token.properties) {
      lineClasses.add(token.properties["GroupId"]);
    }
    
    if (token.kind === "LineBreak") {
      const insertLineOfTokensMessage : InsertCodeLineDataMessage =  {
        directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
        codeLineData: {
          lineNumber: 0,
          lineTokens : tokenLine,
          nodeId : id,
          lineClasses : lineClasses,
          indent : indent,
          diffKind : "NoneDiff"
        } 
      };

      insertLineNumber++;
      insertLineOfTokensMessage.codeLineData.lineNumber = insertLineNumber;
      postMessage(insertLineOfTokensMessage);

      tokenLine.length = 0;
      lineClasses.clear();
    }
    else {
      tokenLine.push(token);
    }
  }

  if (tokenLine.length > 0) {
    const insertLineOfTokensMessage : InsertCodeLineDataMessage =  {
      directive: ReviewPageWorkerMessageDirective.InsertCodeLineData,
      codeLineData: {
        lineNumber: 0,
        lineTokens : tokenLine,
        nodeId : id,
        lineClasses : lineClasses,
        indent : indent,
        diffKind : "NoneDiff"
      } 
    };

    insertLineNumber++;
    insertLineOfTokensMessage.codeLineData.lineNumber = insertLineNumber;
    postMessage(insertLineOfTokensMessage);
  }
}
