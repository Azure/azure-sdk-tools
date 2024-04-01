/// <reference lib="webworker" />

import { ReviewPageWorkerMessageDirective } from "../_models/review";
import { APITreeNode, StructuredToken } from "../_models/revision";

addEventListener('message', ({ data }) => {
  if (data.directive === ReviewPageWorkerMessageDirective.BuildAPITree) {
    let navTreeNodes: any[] = [];
    let treeNodeId : string[] = [];

    data.apiTree.forEach((apiTreeNode: APITreeNode) => {
      navTreeNodes.push(buildAPITree(apiTreeNode as APITreeNode, treeNodeId));
    });

    const message =  {
      directive: ReviewPageWorkerMessageDirective.CreatePageNavigation,
      navTree : navTreeNodes
    };
    postMessage(message);
  }

  if (data.directive === ReviewPageWorkerMessageDirective.BuildTokens) {
    buildTokens(data.apiTreeNode.topTokens, data.apiTreeNodeId, "top");
    buildTokens(data.apiTreeNode.bottomTokens, data.apiTreeNodeId, "bottom");
  }
});


function buildAPITree(apiTreeNode: APITreeNode, treeNodeId : string[], indent: number = 0) : any {
  let idPart = getTokenNodeIdPart(apiTreeNode);
  treeNodeId.push(idPart);
  const nodeId = treeNodeId.join("-");

  let nodeData: any = {
    name: apiTreeNode.properties["Name"],
    id: nodeId,
    indent: indent
  };

  let updateCodeLineMessage =  {
    directive: ReviewPageWorkerMessageDirective.UpdateCodeLines,
    nodeData: nodeData,
  };

  postMessage(updateCodeLineMessage);

  let buildTokensMessage =  { 
    directive: ReviewPageWorkerMessageDirective.PassToTokenBuilder,
    apiTreeNode: apiTreeNode,
    apiTreeNodeId: nodeId
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
    children.push(buildAPITree(child, treeNodeId, indent + 1));
  });

  treeNode.children = children;
  treeNodeId.pop();
  return treeNode;
}

function buildTokens(tokens: StructuredToken[], id: string, appendTo: string) {
  const tokenLine : StructuredToken[] = [];
  for (let token of tokens) {
    if (token.properties["Kind"] === "LineBreak") {
      const message =  {
        directive: ReviewPageWorkerMessageDirective.CreateLineOfTokens,
        tokenLine : tokenLine,
        nodeId : id,
        appendTo: appendTo
      };

      postMessage(message);

      tokenLine.length = 0;
    }
    else {
      tokenLine.push(token);
    }
  }

  if (tokenLine.length > 0) {
    const message =  {
      directive: ReviewPageWorkerMessageDirective.CreateLineOfTokens,
      tokenLine : tokenLine,
      nodeId : id,
      appendTo: appendTo
    };

    postMessage(message);
  }
}

function getTokenNodeIdPart(apiTreeNode: APITreeNode) {
  const kind = apiTreeNode.properties["Kind"];
  const typeKind = apiTreeNode.properties["TypeKind"];
  const methodKind = apiTreeNode.properties["MethodKind"];
  const name = apiTreeNode.properties["Id"];

  let idPart = kind;
  if (typeKind) {
    idPart = `${idPart}_${typeKind}`;
  }

  if (methodKind) {
    idPart = `${idPart}_${methodKind}`;
  }

  if (name) {
    idPart = `${idPart}_${name}`;
  }
  return idPart.toLocaleLowerCase();
}
