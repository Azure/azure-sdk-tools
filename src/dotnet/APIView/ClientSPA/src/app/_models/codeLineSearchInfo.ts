export class CodeLineSearchInfo {
  currentMatch?: number;
  totalMatchCount?: number
}

export class CodeLineSearchMatch {
  rowIndex: number;
  nodeIdHashed: string;
  matchId: number;

  constructor(rowIndex: number, nodeIdHashed: string, matchId: number) {
    this.rowIndex = rowIndex;
    this.nodeIdHashed = nodeIdHashed;
    this.matchId = matchId;
  }
}