import { DiffPair, NodeContext } from 'typescript-codegen-breaking-change-detector';
import { DetectResult } from './DifferenceDetector.js';

export class RestLevelClientDifferencesPostProcessor {
  constructor(
    private detectResult: DetectResult | undefined,
    private baselineInlineNameSet: Map<string, NodeContext>,
    private currentInlineNameSet: Map<string, NodeContext>
  ) {}

  public run() {
    this.handleResult(this.detectResult?.interfaces);
    this.handleResult(this.detectResult?.classes);
    this.handleResult(this.detectResult?.typeAliases);
    this.handleResult(this.detectResult?.functions);
    this.handleResult(this.detectResult?.enums);
  }

  private findCompatibleNodeContext(
    inputContext: NodeContext,
    contextMapToFind: Map<string, NodeContext>
  ): NodeContext | undefined {
    for (const [_, foundContext] of contextMapToFind) {
      const isCompatibleFromInputToFound = inputContext.node.getType().isAssignableTo(foundContext.node.getType());
      const isCompatibleFromFoundToInput = foundContext.node.getType().isAssignableTo(inputContext.node.getType());
      if (isCompatibleFromInputToFound && isCompatibleFromFoundToInput) return foundContext;
    }
    return undefined;
  }
  private tryIgnoreInlineTypes(inputContext: NodeContext, nodeContextMapToFind: Map<string, NodeContext>) {
    if (!inputContext) return false;
    const foundContext = this.findCompatibleNodeContext(inputContext, nodeContextMapToFind);
    if (foundContext) {
      inputContext.used = true;
      foundContext.used = true;
    }
    return true;
  }
  private handleResult(map?: Map<string, DiffPair[]>) {
    map?.forEach((diffPairs, name) => {
      diffPairs = diffPairs.filter((diffPair) => {
        if (!diffPair.source && !diffPair.target) return;

        if (diffPair.source && diffPair.target) {
          const baselineContext = this.baselineInlineNameSet.get(name);
          if (!baselineContext) return false;
          const currentContext = this.currentInlineNameSet.get(name);
          if (!currentContext) return false;
          const baselineType = baselineContext.node.getType();
          const currentType = currentContext.node.getType();
          return !currentType.isAssignableTo(baselineType);
        }

        // current exists
        if (diffPair.source) {
          const currentContext = this.currentInlineNameSet.get(name);
          if (!currentContext) return false;
          const shouldIgnore = this.tryIgnoreInlineTypes(currentContext, this.baselineInlineNameSet);
          return !shouldIgnore;
        }

        // baseline exists
        const targetContext = this.baselineInlineNameSet.get(name);
        if (!targetContext) return false;
        const shouldIgnore = this.tryIgnoreInlineTypes(targetContext, this.currentInlineNameSet);
        return !shouldIgnore;
      });
    });
  }
}
