import { DoublyLinkedListNode } from "../_helpers/doubly-linkedlist";

export class CodeLineSearchInfo {
  currentMatch?: DoublyLinkedListNode<CodeLineSearchMatch> | undefined;
  totalMatchCount?: number;

  constructor(currentMatch: DoublyLinkedListNode<CodeLineSearchMatch> | undefined, totalMatchCount: number | undefined) {
    this.currentMatch = currentMatch;
    this.totalMatchCount = totalMatchCount;
  }
}

export class CodeLineSearchMatch {
  rowIndex: number;
  rowType: string;
  rowPositionInGroup: number;
  nodeIdHashed: string;
  matchId: number;

  constructor(rowIndex: number, rowType: string, rowPositionInGroup: number, nodeIdHashed: string, matchId: number) {
    this.rowIndex = rowIndex;
    this.rowType = rowType;
    this.rowPositionInGroup = rowPositionInGroup;
    this.nodeIdHashed = nodeIdHashed;
    this.matchId = matchId;
  }
}