/// <reference lib="webworker" />

import { BuildTokensMessage, CodeHuskNode, CreateCodeLineHuskMessage, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from "../_models/revision";
import { APITreeNode, StructuredToken } from "../_models/revision";
import { ComputeTokenDiff } from "../_helpers/worker-helpers";

addEventListener('message', ({ data }) => {
  if (data.directive === ReviewPageWorkerMessageDirective.BuildAPITree) {
    let navTreeNodes: any[] = [];
    let treeNodeId : string[] = [];

    data.apiTree.forEach((apiTreeNode: APITreeNode) => {
      navTreeNodes.push(buildAPITree(apiTreeNode as APITreeNode, treeNodeId));
    });

    const createNavigationMessage =  {
      directive: ReviewPageWorkerMessageDirective.CreatePageNavigation,
      navTree : navTreeNodes
    };
    postMessage(createNavigationMessage);
  }
});


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

  let createCodeLineHuskMessage : CreateCodeLineHuskMessage =  {
    directive: ReviewPageWorkerMessageDirective.CreateCodeLineHusk,
    nodeData: nodeData,
  };
  postMessage(createCodeLineHuskMessage);

  let buildTokensMessage : BuildTokensMessage =  { 
    directive: ReviewPageWorkerMessageDirective.PassToTokenBuilder,
    apiTreeNode: apiTreeNode,
    huskNodeId: nodeId,
    position: "top"
  };
  postMessage(buildTokensMessage);

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
    postMessage(createCodeLineHuskMessage);

    buildTokensMessage.position = "bottom";
    postMessage(buildTokensMessage);
  }

  treeNode.children = children;
  treeNodeId.pop();
  return treeNode;
}

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