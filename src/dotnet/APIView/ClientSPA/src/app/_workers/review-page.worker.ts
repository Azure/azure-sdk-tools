/// <reference lib="webworker" />

import { BuildTokensMessage, CodeHuskNode, CreateCodeLineHuskMessage, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective, StructuredTokenKind } from "../_models/revision";
import { APITreeNode, StructuredToken } from "../_models/revision";

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

  if (data.directive === ReviewPageWorkerMessageDirective.BuildTokens) {
    if (data.position === "top") {
      buildTokens(data.apiTreeNode.topTokens, data.huskNodeId, data.position);
    }
    else {
      buildTokens(data.apiTreeNode.bottomTokens, data.huskNodeId, data.position);
    }
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

function buildTokens(tokens: StructuredToken[], id: string, position: string) {
  const tokenLine : StructuredToken[] = [];
  for (let token of tokens) {
    if (token.kind === "LineBreak") {
      const createLinesOfTokensMessage : CreateLinesOfTokensMessage =  {
        directive: ReviewPageWorkerMessageDirective.CreateLineOfTokens,
        tokenLine : tokenLine,
        nodeId : id,
        lineId : createHashFromTokenLine(tokenLine),
        position : position   
      };

      postMessage(createLinesOfTokensMessage);

      tokenLine.length = 0;
    }
    else {
      tokenLine.push(token);
    }
  }

  if (tokenLine.length > 0) {

    const createLinesOfTokensMessage : CreateLinesOfTokensMessage =  {
      directive: ReviewPageWorkerMessageDirective.CreateLineOfTokens,
      tokenLine : tokenLine,
      nodeId : id,
      lineId : createHashFromTokenLine(tokenLine),
      position : position
    };

    postMessage(createLinesOfTokensMessage);
  }
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

function createHashFromTokenLine(tokenLines: StructuredToken[]) {
  const arrayString = JSON.stringify(tokenLines);
  const hash = arrayString.split('').reduce((prevHash, currVal) =>
    ((prevHash << 5) - prevHash) + currVal.charCodeAt(0), 0);

  const cssId = 'id' + hash.toString();

  return cssId;
}
