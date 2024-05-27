/// <reference lib="webworker" />

import { ComputeTokenDiff } from "../_helpers/worker-helpers";
import { CodeDiagnostic, CommentItemModel } from "../_models/review";
import { CodePanelNodeMetaData, CodePanelRowData, CodePanelRowDatatype, DiffLineInProcess, InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective, StructuredToken } from "../_models/revision";
import { APITreeNode, ApiTreeBuilderData, CodePanelData } from "../_models/revision";

let codePanelData: CodePanelData | null = null;
let codePanelRowData: CodePanelRowData[] = [];
let apiTreeBuilderData: ApiTreeBuilderData | null = null;
let lineNumber: number = 0;
let diffLineNumber: number = 0;
let toggleDocumentationClassPart = "bi-arrow-up-square";

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    codePanelData = JSON.parse(jsonString);
    
    buildCodePanelRows("root");
    const codePanelRowDataMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodePanelRowData,
      payload: codePanelRowData
    };
    postMessage(codePanelRowDataMessage);

    const codePanelDataMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodePanelData,
      payload: codePanelData
    };
    postMessage(codePanelDataMessage);

    codePanelData = null;
    codePanelRowData = [];
    apiTreeBuilderData = null;
  }
  else {
    apiTreeBuilderData = data;
    if (apiTreeBuilderData?.showDocumentation) {
      toggleDocumentationClassPart = "bi-arrow-down-square";
    }
  }
});

function buildCodePanelRows(nodeIdHashed: string) {
  const node = codePanelData?.nodeMetaData[nodeIdHashed]!;

  node.documentation.forEach((doc, index) => {
    appendToggleDocumentationClass(node, doc, index);
    setLineNumber(doc);
    if (apiTreeBuilderData?.showDocumentation) {
      codePanelRowData.push(doc);
    }
  });

  node.codeLines.forEach((codeLine, index) => {
    appendToggleDocumentationClass(node, codeLine, index);
    setLineNumber(codeLine);
    codePanelRowData.push(codeLine);
  });

  codePanelRowData.push(...node.diagnostics);
  codePanelRowData.push(...node.commentThread);

  let orderIndex = 0;
  while (orderIndex in node.childrenNodeIdsInOrder) {
    let childNodeIdHashed = node.childrenNodeIdsInOrder[orderIndex];
    buildCodePanelRows(childNodeIdHashed);
    orderIndex++;
  }

  if (node.bottomTokenNodeIdHash) {
    let bottomTokenNode = codePanelData?.nodeMetaData[node.bottomTokenNodeIdHash]!;
    bottomTokenNode.codeLines.forEach((codeLine, index) => {
      appendToggleDocumentationClass(node, codeLine, index);
      setLineNumber(codeLine);
      codePanelRowData.push(codeLine);
    });
  }
}

function appendToggleDocumentationClass(node: CodePanelNodeMetaData, codePanelRow: CodePanelRowData, index: number) {
  if (node.documentation.length > 0 && codePanelRow.type === CodePanelRowDatatype.CodeLine && index == 0) {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show`;
  } else {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;
  }
}

function setLineNumber(row: CodePanelRowData) {
  if (row.diffKind === "Removed") {
    row.lineNumber = ++lineNumber;
  } else if (row.diffKind === "Added") {
    lineNumber++;
    diffLineNumber++;
    row.lineNumber = diffLineNumber;
  } else {
    lineNumber++;
    diffLineNumber++;
    row.lineNumber = lineNumber;
  }
}
