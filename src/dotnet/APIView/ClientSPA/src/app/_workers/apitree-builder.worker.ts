/// <reference lib="webworker" />

import { ReviewContent } from "../_models/review";
import { BuildTokensMessage, CodeHuskNode, CreateCodeLineHuskMessage, ReviewPageWorkerMessageDirective } from "../_models/revision";
import { APITreeNode } from "../_models/revision";

let interWorkerPort : MessagePort;
let lastCodeLineHusk : CodeHuskNode;

addEventListener('message', ({ data }) => {
  if (data.interWorkerPort) {
    interWorkerPort = data.interWorkerPort;
  }
  else {
    if (data instanceof ArrayBuffer) {
      let jsonString = new TextDecoder().decode(new Uint8Array(data));

      let reviewContent: ReviewContent = JSON.parse(jsonString);
      const updateReviewModelMessage = {
        directive: ReviewPageWorkerMessageDirective.UpdateReviewModel,
        reviewModel : reviewContent.review
      };

      postMessage(updateReviewModelMessage);

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

      let createCodeLineHuskMessage : CreateCodeLineHuskMessage =  {
        directive: ReviewPageWorkerMessageDirective.CreateCodeLineHusk,
        nodeData: lastCodeLineHusk,
        isLastHuskNode: true
      };
      postMessage(createCodeLineHuskMessage);
    }
  }
});

function postMessageToTokenBuilder(message: any) {
  interWorkerPort.postMessage(message);
}

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
  let idPart = getTokenNodeIdPart(apiTreeNode);
  treeNodeId.push(idPart);
  const nodeId = treeNodeId.join("-");

  let nodeData: CodeHuskNode = {
    name: apiTreeNode.name,
    id: nodeId,
    indent: indent,
    position: "top"
  };
  lastCodeLineHusk = nodeData;

  let createCodeLineHuskMessage : CreateCodeLineHuskMessage =  {
    directive: ReviewPageWorkerMessageDirective.CreateCodeLineHusk,
    nodeData: nodeData,
    isLastHuskNode: false
  };
  postMessage(createCodeLineHuskMessage);

  let buildTokensMessage : BuildTokensMessage =  { 
    apiTreeNode: apiTreeNode,
    huskNodeId: nodeId,
    position: "top"
  };
  postMessageToTokenBuilder(buildTokensMessage);

  let treeNode: any = {
    label: nodeData.name,
    data: nodeData,
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
    nodeData.position = "bottom";
    createCodeLineHuskMessage.nodeData = nodeData;
    lastCodeLineHusk = nodeData;
    postMessage(createCodeLineHuskMessage);

    buildTokensMessage.position = "bottom";
    postMessageToTokenBuilder(buildTokensMessage);
  }

  treeNode.children = children;
  treeNodeId.pop();
  return treeNode;
}

/**
 * Uses the properties of the node to create an Id that is guaranteed to be unique
 * @param apiTreeNode 
 */
function getTokenNodeIdPart(apiTreeNode: APITreeNode) {
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
  return idPart.toLocaleLowerCase();
}