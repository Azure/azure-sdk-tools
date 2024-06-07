import { ApiTreeNode } from "./api-tree-node.js";
import { ApiView } from "./apiview.js";
import { ApiViewDiagnostic } from "./diagnostic.js";

/**
 * The serializable form of the ApiView. This is the tokenfile that gets sent to the APIView service.
 */
export class ApiViewDocument {
  name: string;
  packageName: string;
  apiForest: ApiTreeNode[] | null;
  diagnostics: ApiViewDiagnostic[];
  versionString: string;
  language: string;
  crossLanguagePackageId: string | undefined;

  constructor(apiview: ApiView) {
    this.name = apiview.name;
    this.packageName = apiview.packageName;
    this.apiForest = apiview.nodes;
    this.diagnostics = apiview.diagnostics;
    this.versionString = apiview.versionString;
    this.language = "TypeSpec";
    this.crossLanguagePackageId = apiview.crossLanguagePackageId;
  }

  asString(): string {
    return JSON.stringify(this, null, 2);
  }
}
