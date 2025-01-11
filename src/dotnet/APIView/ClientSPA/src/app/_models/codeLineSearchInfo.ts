export class CodeLineSearchInfo {
  currentMatch?: number;
  totalMatchCount?: number
}

export class CodeLineSearchMatch {
  nodeIdHashed: string;
  matchId: number;

  constructor(nodeIdHashed: string, matchId: number) {
    this.nodeIdHashed = nodeIdHashed;
    this.matchId = matchId;
  }
}