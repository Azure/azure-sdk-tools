/// <reference lib="webworker" />

import { AppendTokenLinesToMessage, CreateLinesOfTokensMessage, ReviewPageWorkerMessageDirective } from "../_models/revision";
import { APITreeNode, StructuredToken } from "../_models/revision";
import { ComputeTokenDiff } from "../_helpers/worker-helpers";

let interWorkerPort : MessagePort;

addEventListener('message', ({ data }) => {
  if (data.interWorkerPort) {
    interWorkerPort = data.interWorkerPort
    interWorkerPort.onmessage = handleMessageFromInterWorkerPort;
  }
});

function handleMessageFromInterWorkerPort({ data }: { data: any }) {
  buildTokens(data.apiTreeNode, data.huskNodeId, data.position);
}

function buildTokens(apiTreeNode: APITreeNode, id: string, position: string) {
  if (apiTreeNode.diffKind === "NoneDiff") {
    buildTokensForNonDiffNodes(apiTreeNode, id, position);
  }
  else {
    buildTokensForDiffNodes(apiTreeNode, id, position);
  }

  const message : AppendTokenLinesToMessage = { 
    directive : ReviewPageWorkerMessageDirective.AppendTokenLinesToNode,
    nodeId: id,
    position: position
  };
  postMessage(message);
}

function createHashFromTokenLine(tokenLines: StructuredToken[]) {
  const arrayString = JSON.stringify(tokenLines);
  const hash = arrayString.split('').reduce((prevHash, currVal) =>
    ((prevHash << 5) - prevHash) + currVal.charCodeAt(0), 0);

  const cssId = 'id' + hash.toString();

  return cssId;
}

function buildTokensForDiffNodes(apiTreeNode: APITreeNode, id: string, position: string) {
  const beforeTokens = (position === "top") ? apiTreeNode.topTokens : apiTreeNode.bottomTokens;
  const afterTokens = (position === "top") ? apiTreeNode.topDiffTokens : apiTreeNode.bottomDiffTokens;

  let beforeIndex = 0;
  let afterIndex = 0;

  while (beforeIndex < beforeTokens.length || afterIndex < afterTokens.length) {
    const beforeTokenLine : Array<StructuredToken> = [];
    const afterTokenLine : Array<StructuredToken> = [];

    while (beforeIndex < beforeTokens.length) {
      const token = beforeTokens[beforeIndex++];
      if (token.kind === "LineBreak") {
        break;
      }
      beforeTokenLine.push(token);
    }

    while (afterIndex < afterTokens.length) {
      const token = afterTokens[afterIndex++];
      if (token.kind === "LineBreak") {
        break;
      }
      afterTokenLine.push(token);
    }

    const diffTokenLineResult = ComputeTokenDiff(beforeTokenLine, afterTokenLine) as [StructuredToken[], StructuredToken[], boolean];

    let createLinesOfTokensMessage : CreateLinesOfTokensMessage =  {
      directive: ReviewPageWorkerMessageDirective.CreateLineOfTokens,
      tokenLine : diffTokenLineResult[0],
      nodeId : id,
      lineId : createHashFromTokenLine(diffTokenLineResult[0]),
      position : position,
      diffKind : "Unchanged"
    };

    if (diffTokenLineResult[2] === true) {
      createLinesOfTokensMessage.diffKind = "Removed";

      postMessage(createLinesOfTokensMessage);

      createLinesOfTokensMessage.tokenLine = diffTokenLineResult[1];
      createLinesOfTokensMessage.lineId = createHashFromTokenLine(diffTokenLineResult[1]);
      createLinesOfTokensMessage.diffKind = "Added";

      postMessage(createLinesOfTokensMessage);
    }
    else {
      postMessage(createLinesOfTokensMessage);
    }
  }
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
        position : position,
        diffKind : "NoneDiff"  
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
      position : position,
      diffKind : "NoneDiff"
    };

    postMessage(createLinesOfTokensMessage);
  }
}
