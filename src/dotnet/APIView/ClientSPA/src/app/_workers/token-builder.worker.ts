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

  // This message is used to signal the code-panel component that the node is ready to be displayed. i.e. all the token lines have been built
  // The code-panel component will then append the tokens to the node.
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

/**
 * Build the tokens for if the node potentially contains diff
 * @param apiTreeNode 
 * @param id 
 * @param position 
 */
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
      position : position,
      diffKind : "Unchanged"
    };

    if (diffTokenLineResult[2] === true) {
      createLinesOfTokensMessage.diffKind = "Removed";

      postMessage(createLinesOfTokensMessage);

      createLinesOfTokensMessage.tokenLine = diffTokenLineResult[1];
      createLinesOfTokensMessage.diffKind = "Added";

      postMessage(createLinesOfTokensMessage);
    }
    else {
      postMessage(createLinesOfTokensMessage);
    }
  }
}

/**
 * Build the tokens for if the node contains no diff
 * @param apiTreeNode 
 * @param id 
 * @param position 
 */
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
      position : position,
      diffKind : "NoneDiff"
    };

    postMessage(createLinesOfTokensMessage);
  }
}
