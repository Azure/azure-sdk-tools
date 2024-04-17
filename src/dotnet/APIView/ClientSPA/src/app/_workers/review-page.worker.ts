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
    buildTokens(data.apiTreeNode, data.huskNodeId, data.position);
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

function buildTokens(apiTreeNode: APITreeNode, id: string, position: string) {
  if (apiTreeNode.diffKind === "NonDiff") {
    buildTokensForNonDiffNodes(apiTreeNode, id, position);
  }
  else {
    buildTokensForDiffNodes(apiTreeNode, id, position);
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

function buildTokensForDiffNodes(apiTreeNode: APITreeNode, id: string, position: string) {
  const tokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;

}

export function ComputeTokenDiff(beforeTokens: any[], afterTokens: any[]) : any[] {
  const diffResult = [];

  for (let i = 0; i < Math.max(beforeTokens.length, afterTokens.length); i++) {
    if (i >= beforeTokens.length) {
      let token = afterTokens[i];
      token.diffKind = "Added";
      diffResult.push(token);
    }
    else if (i >= afterTokens.length) {
      let token = beforeTokens[i];
      token.diffKind = "Removed";
      diffResult.push(token);
    }
    else if ((beforeTokens[i].value !== afterTokens[i].value) || (beforeTokens[i].id !== afterTokens[i].id)) {
      let token = beforeTokens[i];
      token.diffKind = "Removed";
      diffResult.push(token);
      token = afterTokens[i];
      token.diffKind = "Added";
      diffResult.push(token);
    }
    else {
      let token = beforeTokens[i];
      token.diffKind = "Unchanged";
      diffResult.push(token);
    }
  }

  return diffResult;
}

function buildTokensForNonDiffNodes(apiTreeNode: APITreeNode, id: string, position: string)
{
  const tokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
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
