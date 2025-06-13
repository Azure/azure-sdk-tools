import { format } from "string-template";
import { DetectContext, DetectResult } from "./DifferenceDetector.js";
import {
    DiffLocation,
    DiffPair,
    DiffReasons,
} from "typescript-codegen-breaking-change-detector";
import { InterfaceDeclaration, SyntaxKind } from "ts-morph";
import { SDKType } from "../../common/types.js";

export interface ChangelogItems {
    features: string[];
    breakingChanges: string[];
}

export class ChangelogGenerator {
    private operationGroupAddedTemplate =
        "Added operation group {interfaceName}";
    private operationGroupRemovedTemplate =
        "Removed operation group {interfaceName}";

    changelogItems: ChangelogItems = {
        features: [],
        breakingChanges: [],
    };

    constructor(
        private detectContext: DetectContext,
        private detectResult: DetectResult,
    ) {}

    public generate(): ChangelogItems {
        this.generateForInterfaces();
        return changelogItems;
    }

    // TODO: handle cross SDK type differences
    private isOperationGroupDiff(baseline: InterfaceDeclaration | undefined, current: InterfaceDeclaration | undefined): boolean {
        switch (this.detectContext.sdkTypes.current) {
            case SDKType.HighLevelClient: {
                const isOperationGroup = (
                    node: InterfaceDeclaration | undefined,
                ): boolean =>
                    (node &&
                        node.getProperties().length === 0 &&
                        node.getMembers().length > 0) ??
                    false;
                return isOperationGroup(baseline) || isOperationGroup(current);
            }
            case SDKType.ModularClient: {
                return current?.getName().endsWith("Operations") ?? baseline?.getName().endsWith("Operations") ?? false;
            }
            case SDKType.RestLevelClient:
                // TODO: double check if this is correct
                return true;
            default:
                throw new Error(
                    `Unsupported SDK type: ${this.detectContext.sdkTypes.current} for operation groups.`,
                );
        }
    }

    private generateForOperationGroupLevelDiff(
        name: string,
        diffPairs: DiffPair[],
    ): void {
        const isAdded = diffPairs.some(
            (p) =>
                p.location === DiffLocation.Interface &&
                p.reasons & DiffReasons.Added,
        );
        const isRemoved = diffPairs.some(
            (p) =>
                p.location === DiffLocation.Interface &&
                p.reasons & DiffReasons.Removed,
        );
        if (isAdded)
            this.changelogItems.features.push(
                format(this.operationGroupAddedTemplate, {
                    interfaceName: name,
                }),
            );

        if (isRemoved) {
            this.changelogItems.breakingChanges.push(
                format(this.operationGroupRemovedTemplate, {
                    interfaceName: name,
                }),
            );
        }
    }

    private generateForInterfaces() {
        this.detectResult.interfaces.forEach((diffPairs, name) => {
            const current = this.detectContext.context.current.getInterface(name);
            const baseline = this.detectContext.context.baseline.getInterface(name);
            if (this.isOperationGroupDiff(baseline, current)) {
                this.generateForOperationGroupLevelDiff(name, diffPairs);
                this.generateForOperationLevelDiff(name, diffPairs);
                return;
            }
        });
    }

    
}
