/// <reference lib="webworker" />
import 'reflect-metadata';
import { CodePanelNodeMetaData, CodePanelRowData, CodePanelRowDatatype, InsertCodePanelRowDataMessage, NavigationTreeNode, ReviewPageWorkerMessageDirective } from "../_models/revision";
import { ApiTreeBuilderData, CodePanelData } from "../_models/revision";
import { plainToClass } from 'class-transformer';

let codePanelData: CodePanelData | null = null;
let codePanelRowData: CodePanelRowData[] = [];
let navigationTree : NavigationTreeNode [] = [];
let apiTreeBuilderData: ApiTreeBuilderData | null = null;
let diffBuffer: CodePanelRowData[] = [];
let lineNumber: number = 0;
let diffLineNumber: number = 0;
let toggleDocumentationClassPart = "bi-arrow-up-square";

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    codePanelData = JSON.parse(jsonString);
    
    buildCodePanelRows("root", navigationTree);
    const codePanelRowDataMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodePanelRowData,
      payload: codePanelRowData
    };

    postMessage(codePanelRowDataMessage);

    const navigationTreeMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.CreatePageNavigation,
      payload: navigationTree
    };
    postMessage(navigationTreeMessage);

    const codePanelDataMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodePanelData,
      payload: codePanelData
    };
    postMessage(codePanelDataMessage);

    codePanelData = null;
    codePanelRowData = [];
    navigationTree = [];
    diffBuffer = [];
    apiTreeBuilderData = null;
  }
  else {
    apiTreeBuilderData = data;
    if (apiTreeBuilderData?.showDocumentation) {
      toggleDocumentationClassPart = "bi-arrow-down-square";
    }
  }
});

function buildCodePanelRows(nodeIdHashed: string, navigationTree: NavigationTreeNode []) {
  const rawNode = codePanelData?.nodeMetaData[nodeIdHashed]!;
  const node = plainToClass(CodePanelNodeMetaData, rawNode);
  codePanelData!.nodeMetaData[nodeIdHashed] = node;

  let buildNode = true;
  let buildChildren = true;
  let addNodeToBuffer = false
 
  if (nodeIdHashed !== "root" && (apiTreeBuilderData?.diffStyle === "trees" || apiTreeBuilderData?.diffStyle === "nodes") && !node.isNodeWithDiffInDescendants) {
    buildNode = false;
    buildChildren = false;
  }

  if (!buildNode && Object.keys(node.childrenNodeIdsInOrder).length === 0 && 
    (apiTreeBuilderData?.diffStyle !== "nodes" || node.isNodeWithDiff)) {
    buildNode = true;
  }

  if (!node.isNodeWithDiff && apiTreeBuilderData?.diffStyle === "nodes" && Object.keys(node.childrenNodeIdsInOrder).length === 0) {
    addNodeToBuffer = true;
  }

  let navigationChildren = navigationTree;
  if (node.navigationTreeNode) {
    if (!node.navigationTreeNode.children) {
      node.navigationTreeNode.children = [];
    }
    navigationChildren = node.navigationTreeNode.children;
  }

  if (node.childrenNodeIdsInOrder && Object.keys(node.childrenNodeIdsInOrder).length === 0 && node.isNodeWithDiff) {
    codePanelRowData.push(...diffBuffer);
    diffBuffer = [];
  }

  if (node.documentation) {
    node.documentation.forEach((doc, index) => {
      appendToggleDocumentationClass(node, doc, index);
      setLineNumber(doc);
      if (buildNode && apiTreeBuilderData?.showDocumentation) {
        codePanelRowData.push(doc);
      }
    });
  }

  if (node.codeLines) {
    node.codeLines.forEach((codeLine, index) => {
      appendToggleDocumentationClass(node, codeLine, index);
      setLineNumber(codeLine);
      if (buildNode) {
        codePanelRowData.push(codeLine);
      }
      if (addNodeToBuffer) {
        diffBuffer.push(codeLine);
        addJustDiffBuffer();
      }
    });
  }

  if (buildNode && node.diagnostics) {
    codePanelRowData.push(...node.diagnostics);
  }

  if (buildNode && node.commentThread) {
    codePanelRowData.push(...node.commentThread);
  }
  
  if (buildChildren) {
    let orderIndex = 0;
    while (node.childrenNodeIdsInOrder && orderIndex in node.childrenNodeIdsInOrder) {
      let childNodeIdHashed = node.childrenNodeIdsInOrder[orderIndex];
      buildCodePanelRows(childNodeIdHashed, navigationChildren);
      orderIndex++;
    }
  }

  if (buildNode && node.navigationTreeNode) {
    navigationTree.push(node.navigationTreeNode);
  }  

  if (node.bottomTokenNodeIdHash) {
    codePanelRowData.push(...diffBuffer);
    diffBuffer = [];

    let bottomTokenRawNode = codePanelData?.nodeMetaData[node.bottomTokenNodeIdHash]!;
    let bottomTokenNode = plainToClass(CodePanelNodeMetaData, bottomTokenRawNode);
    codePanelData!.nodeMetaData[node.bottomTokenNodeIdHash] = bottomTokenNode;

    if (bottomTokenNode.codeLines) {
      bottomTokenNode.codeLines.forEach((codeLine, index) => {
        appendToggleDocumentationClass(node, codeLine, index);
        setLineNumber(codeLine);
        if (buildNode) {
          codePanelRowData.push(codeLine);
        }
      });
    }
  }
}

function appendToggleDocumentationClass(node: CodePanelNodeMetaData, codePanelRow: CodePanelRowData, index: number) {
  if (node.documentation && node.documentation.length > 0 && codePanelRow.type === CodePanelRowDatatype.CodeLine && index == 0 && codePanelRow.rowOfTokensPosition === "top") {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show`;
  } else {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;
  }
}

function setLineNumber(row: CodePanelRowData) {
  if (row.diffKind === "removed") {
    row.lineNumber = ++lineNumber;
  } else if (row.diffKind === "added") {
    lineNumber++;
    diffLineNumber++;
    row.lineNumber = diffLineNumber;
  } else {
    lineNumber++;
    diffLineNumber++;
    row.lineNumber = lineNumber;
  }
}

function addJustDiffBuffer() {
  if (diffBuffer.length > 3) {
    diffBuffer.shift();
  }
}