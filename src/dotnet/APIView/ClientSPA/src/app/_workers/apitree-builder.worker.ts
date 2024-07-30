/// <reference lib="webworker" />
import 'reflect-metadata';
import { CodePanelNodeMetaData, InsertCodePanelRowDataMessage, NavigationTreeNode, ReviewPageWorkerMessageDirective } from "../_models/revision";
import { ApiTreeBuilderData, CodePanelData } from "../_models/revision";
import { CodePanelRowData, CodePanelRowDatatype } from '../_models/codePanelRowData';

let codePanelData: CodePanelData | null = null;
let codePanelRowData: CodePanelRowData[] = [];
let navigationTree : NavigationTreeNode [] = [];
let apiTreeBuilderData: ApiTreeBuilderData | null = null;
let diffBuffer: CodePanelRowData[] = [];
let lineNumber: number = 0;
let diffLineNumber: number = 0;
let toggleDocumentationClassPart = "bi-arrow-up-square";
let hasHiddenAPI: boolean = false;

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

    const hasHiddenAPIMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.SetHasHiddenAPIFlag,
      payload: hasHiddenAPI
    };
    postMessage(hasHiddenAPIMessage);

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
  const node = codePanelData?.nodeMetaData[nodeIdHashed]!;

  let buildNode = true;
  let buildChildren = true;
  let addNodeToBuffer = false
 
  if (nodeIdHashed !== "root" && (apiTreeBuilderData?.diffStyle === "trees" || apiTreeBuilderData?.diffStyle === "nodes") && !node.isNodeWithDiffInDescendants) {
    buildNode = false;
    buildChildren = false;
  }

  if (!buildNode && (!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0) && 
    (apiTreeBuilderData?.diffStyle !== "nodes" || node.isNodeWithDiff)) {
    buildNode = true;
  }

  if (!node.isNodeWithDiff && apiTreeBuilderData?.diffStyle === "nodes" && (!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0)) {
    addNodeToBuffer = true;
  }

  let navigationChildren = navigationTree;
  if (node.navigationTreeNode) {
    if (!node.navigationTreeNode.children) {
      node.navigationTreeNode.children = [];
    }
    navigationChildren = node.navigationTreeNode.children;
  }

  if ((!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0) && node.isNodeWithDiff) {
    codePanelRowData.push(...diffBuffer);
    diffBuffer = [];
  }

  if (node.documentation) {
    node.documentation.forEach((doc, index) => {
      if (shouldAppendIfRowIsHiddenAPI(doc)) {
        doc.rowClasses = new Set<string>(doc.rowClasses); // Ensure that the rowClasses is a Set
        appendToggleDocumentationClass(node, doc, index);
        setLineNumber(doc);
        if (buildNode && apiTreeBuilderData?.showDocumentation) {
          codePanelRowData.push(doc);
        }
      }
    });
  }

  if (node.codeLines) {
    node.codeLines.forEach((codeLine, index) => {
      if (shouldAppendIfRowIsHiddenAPI(codeLine)) {
        if (index === node.codeLines.length - 1 && node.diagnostics && node.diagnostics.length > 0) { // last row of toptoken codeLines
          codeLine.toggleCommentsClasses = codeLine.toggleCommentsClasses.replace("can-show", "show").replace("hide", "show"); // show comment indicatior node has diagnostic comments
        }
        codeLine.rowClasses = new Set<string>(codeLine.rowClasses); // Ensure that the rowClasses is a Set
        appendToggleDocumentationClass(node, codeLine, index);
        setLineNumber(codeLine);
        if (buildNode) {
          codePanelRowData.push(codeLine);
        }
        if (addNodeToBuffer) {
          diffBuffer.push(codeLine);
          addJustDiffBuffer();
        }
      }
    });
  }

  if (buildNode && node.diagnostics && apiTreeBuilderData?.showSystemComments) {
    node.diagnostics.forEach((diag, index) => {
      if (shouldAppendIfRowIsHiddenAPI(diag)) {
        diag.rowClasses = new Set<string>(diag.rowClasses); // Ensure that the rowClasses is a Set
        codePanelRowData.push(diag);
      }
    });
  }

  if (buildNode && node.commentThread && apiTreeBuilderData?.showComments) {
    Object.keys(node.commentThread).map(Number).forEach((key) => {
      const comment: CodePanelRowData = node.commentThread[key];
      if (shouldAppendIfRowIsHiddenAPI(comment)) {
        comment.rowClasses = new Set<string>(comment.rowClasses); // Ensure that the rowClasses is a Set
        codePanelRowData.push(comment);
      }
    });
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

    let bottomTokenNode = codePanelData?.nodeMetaData[node.bottomTokenNodeIdHash]!;

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

function shouldAppendIfRowIsHiddenAPI(row: CodePanelRowData) {
  if (row.isHiddenAPI) {
    hasHiddenAPI = true;
    return apiTreeBuilderData?.showHiddenApis;
  } else {
    return true;
  }
}