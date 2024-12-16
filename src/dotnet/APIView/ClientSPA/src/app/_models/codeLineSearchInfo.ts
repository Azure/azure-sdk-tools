export class CodeLineSearchInfo {
  current: number
  total: number
  matchedNodeIds: Set<string>

  constructor() {
    this.current = 0
    this.total = 0
    this.matchedNodeIds = new Set<string>();
  }
}