/// <reference lib="webworker" />
import 'reflect-metadata';
import { ApiTreeBuilderData } from "../_models/revision";
import { CodePanelData, CodePanelNodeMetaData, CodePanelRowData, CodePanelRowDatatype } from '../_models/codePanelModels';
import { InsertCodePanelRowDataMessage, ReviewPageWorkerMessageDirective } from '../_models/insertCodePanelRowDataMessage';
import { NavigationTreeNode } from '../_models/navigationTreeModels';
import { DIFF_ADDED, DIFF_REMOVED, FULL_DIFF_STYLE, NODE_DIFF_STYLE, TREE_DIFF_STYLE } from '../_helpers/common-helpers';

let codePanelData: CodePanelData | null = null;
let codePanelRowData: CodePanelRowData[] = [];
let navigationTree : NavigationTreeNode [] = [];
let apiTreeBuilderData: ApiTreeBuilderData | null = null;
let diffBuffer: CodePanelRowData[] = [];
let lineNumber: number = 0;
let diffLineNumber: number = 0;
let toggleDocumentationClassPart = "bi-arrow-up-square";
let hasHiddenAPI: boolean = false;
let visibleNodes: Set<string> = new Set<string>();
let addPostDiffContext: boolean = false;
let isNavigationTreeCreated: boolean = false;

addEventListener('message', ({ data }) => {
  if (data instanceof ArrayBuffer) {
    let jsonString = new TextDecoder().decode(new Uint8Array(data));

    codePanelData = JSON.parse(jsonString);
    if (!codePanelData?.hasDiff) {
      apiTreeBuilderData!.diffStyle = FULL_DIFF_STYLE; // If there is no diff nodes and tree diff will not work
    }
        
    buildCodePanelRows("root", navigationTree);
    const codePanelRowDataMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.UpdateCodePanelRowData,
      payload: codePanelRowData
    };

    if (codePanelData?.navigationTreeNodes && codePanelData?.navigationTreeNodes.length > 0)
    {
      isNavigationTreeCreated = true;
      navigationTree = codePanelData?.navigationTreeNodes;
      //Remove navigation nodes for nodes that are not visible in diff style view
      navigationTree.forEach(node => FilterVisibleNavigationNodes(node));
      navigationTree = navigationTree.filter(n => n.visible);
    }

    postMessage(codePanelRowDataMessage);

    const navigationTreeMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.CreatePageNavigation,
      payload: navigationTree
    };
    postMessage(navigationTreeMessage);

    const hasHiddenAPIMessage : InsertCodePanelRowDataMessage = {
      directive: ReviewPageWorkerMessageDirective.SetHasHiddenAPIFlag,
      payload: hasHiddenAPI,
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
    visibleNodes = new Set<string>();
    addPostDiffContext = false;
    isNavigationTreeCreated = false;
  }
  else {
    apiTreeBuilderData = data;
    if (apiTreeBuilderData?.showDocumentation) {
      toggleDocumentationClassPart = "bi-arrow-down-square";
    }
  }
});

function buildCodePanelRows(nodeIdHashed: string, navigationTree: NavigationTreeNode [], isParentNodeWithDiff: boolean = false) {
  const node = codePanelData?.nodeMetaData[nodeIdHashed]!;

  if(node.isProcessed)
    return;

  //If current node is related line attribute and then related node is not modified then skip current node in tree and node view
  if (node.relatedNodeIdHash && !node.isNodeWithDiff && !node.isNodeWithDiffInDescendants && 
    (apiTreeBuilderData?.diffStyle == TREE_DIFF_STYLE || apiTreeBuilderData?.diffStyle == NODE_DIFF_STYLE))
  {
    let relatedNode = codePanelData?.nodeMetaData[node.relatedNodeIdHash]!;
    if (!relatedNode.isNodeWithDiff && !node.isNodeWithDiffInDescendants && !visibleNodes.has(node.relatedNodeIdHash))
    {
      return;
    }
  }

  let buildNode = true;
  let buildChildren = true;
  let addNodeToBuffer = false
 
  if (nodeIdHashed !== "root" && (apiTreeBuilderData?.diffStyle === TREE_DIFF_STYLE || apiTreeBuilderData?.diffStyle === NODE_DIFF_STYLE) && 
    (!node.isNodeWithDiffInDescendants || (!apiTreeBuilderData?.showDocumentation && !node.isNodeWithNoneDocDiffInDescendants))) {
    buildNode = false;
    buildChildren = false;
  }
    
  if (!buildNode && (!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0) && 
    (apiTreeBuilderData?.diffStyle !== NODE_DIFF_STYLE || node.isNodeWithDiff)) {
    buildNode = true;
  }

  if (isParentNodeWithDiff && !node.isNodeWithDiff && apiTreeBuilderData?.diffStyle === NODE_DIFF_STYLE && (!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0)) {
    addNodeToBuffer = true;
  }

  let navigationChildren = navigationTree;
  if (!isNavigationTreeCreated && node.navigationTreeNode) {
    if (!node.navigationTreeNode.children) {
      node.navigationTreeNode.children = [];
    }
    navigationChildren = node.navigationTreeNode.children;
  } 

  if ((!node.childrenNodeIdsInOrder || Object.keys(node.childrenNodeIdsInOrder).length === 0) && node.isNodeWithDiff) {
    codePanelRowData.push(...diffBuffer);
    diffBuffer.map(row => visibleNodes.add(row.nodeIdHashed));
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
        if (index === node.codeLines.length - 1 && node.diagnostics && node.diagnostics.length > 0) { // last row of top token codeLines
          codeLine.toggleCommentsClasses = codeLine.toggleCommentsClasses.replace("can-show", "show").replace("hide", "show"); // show comment indicator node has diagnostic comments
        }
        codeLine.rowClasses = new Set<string>(codeLine.rowClasses); // Ensure that the rowClasses is a Set
        appendToggleDocumentationClass(node, codeLine, index);
        setLineNumber(codeLine);
        if (buildNode) {
          codePanelRowData.push(codeLine);
          visibleNodes.add(nodeIdHashed);
          addPostDiffContext = true;
        }
        if (addNodeToBuffer) {
          // We should add immediate 3 lines as context post a changed line
          if (addPostDiffContext && diffBuffer.length === 3)
          {
            codePanelRowData.push(...diffBuffer);
            diffBuffer.map(row => visibleNodes.add(row.nodeIdHashed));
            diffBuffer = [];
            addPostDiffContext = false;
          }
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
      buildCodePanelRows(childNodeIdHashed, navigationChildren, node.isNodeWithDiff || node.isNodeWithDiffInDescendants);
      orderIndex++;
    }
  }

  if (buildNode && node.navigationTreeNode && !isNavigationTreeCreated) {
    navigationTree.push(node.navigationTreeNode);
  }  

  if (node.bottomTokenNodeIdHash) {
    codePanelRowData.push(...diffBuffer);
    diffBuffer = [];

    let bottomTokenNode = codePanelData?.nodeMetaData[node.bottomTokenNodeIdHash]!;

    if (bottomTokenNode.codeLines) {
      bottomTokenNode.codeLines.forEach((codeLine, index) => {
        if (shouldAppendIfRowIsHiddenAPI(codeLine)) {
          codeLine.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;
          setLineNumber(codeLine);
          if (buildNode) {
            codePanelRowData.push(codeLine);
            visibleNodes.add(codeLine.nodeIdHashed);
          }
        }
      });
    }
    bottomTokenNode.isProcessed = true;
  }
}

function appendToggleDocumentationClass(node: CodePanelNodeMetaData, codePanelRow: CodePanelRowData, index: number) {
  if (node.documentation && node.documentation.length > 0 && codePanelRow.type === CodePanelRowDatatype.CodeLine && index == 0) {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} can-show`;
  } else {
    codePanelRow.toggleDocumentationClasses = `bi ${toggleDocumentationClassPart} hide`;
  }
}

function setLineNumber(row: CodePanelRowData) {
  if (row.diffKind === DIFF_REMOVED) {
    row.lineNumber = ++lineNumber;
  } else if (row.diffKind === DIFF_ADDED) {
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
    return apiTreeBuilderData?.showHiddenApis || codePanelData?.hasHiddenAPIThatIsDiff;
  } else {
    return true;
  }
}

function FilterVisibleNavigationNodes(node: NavigationTreeNode) {
  // Recursively perform a bottom up traversal and trim down any invisible nodes
  if (node.children) {
    for (let child of node.children) {
      FilterVisibleNavigationNodes(child);
    }
    node.children = node.children.filter(c => c.visible);
  }

  if (visibleNodes.has(node.data.nodeIdHashed) || (node.children && node.children.some(c => c.visible))) {
    node.visible = true;
  }
}