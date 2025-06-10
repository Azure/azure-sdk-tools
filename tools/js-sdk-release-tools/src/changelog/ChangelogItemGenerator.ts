import { SyntaxKind } from "ts-morph";
import {
    DiffLocation,
    DiffPair,
    DiffReasons,
} from "typescript-codegen-breaking-change-detector";
import { SDKType } from "../common/types.js";

// TODO: handle higher level client to modular client migration

// NOTE: operation group is an interface
// - High level client: interface that only contains member functions
// - Modular client: interface that only contains arrow functions and end with "Operations"
export class ChangelogItemGenerator {
    constructor(private diffPairs: DiffPair[], private sdkType: SDKType) {}

    private getDiffForOperationGroups(): string[] {
        switch (this.sdkType) {
            case SDKType.HighLevelClient:
                return this.diffPairs.filter((p) => {
                    p.location === DiffLocation.Interface && isPropertyMethod;
                    p.source?.node;
                });
        }
    }
}
