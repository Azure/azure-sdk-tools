import { ApiTreeNode } from "./api-tree-node.js";
import { ApiView } from "../apiview.js";
import { CodeDiagnostic } from "./code-diagnostic.js";
import { LIB_VERSION } from "../version.js";
import { ApiViewSerializable } from "../interface.js";

/**
 * The serializable form of the ApiView. This is the tokenfile that gets sent to the APIView service.
 */
export class CodeFile implements ApiViewSerializable {
  parserVersion: string;
  name: string;
  language: string;
  packageName: string;
  packageVersion: string;

  crossLanguagePackageId: string | undefined;
  apiForest: ApiTreeNode[] | null;
  diagnostics: CodeDiagnostic[];

  constructor(apiview: ApiView) {
    this.parserVersion = LIB_VERSION;
    this.name = apiview.name;
    this.packageName = apiview.packageName;
    this.packageVersion = apiview.packageVersion
    this.apiForest = apiview.nodes;
    this.diagnostics = apiview.diagnostics;
    this.language = "TypeSpec";
    this.crossLanguagePackageId = apiview.crossLanguagePackageId;
  }

  asString(): string {
    return JSON.stringify(this, null, 2);
  }

  serialize(): object {
    return {
      VersionString: this.parserVersion,
      Name: this.name,
      Language: "TypeSpec",
      LanguageVariant: null,
      PackageName: this.packageName,
      PackageVersion: this.packageVersion,
      ServiceName: null,
      CrossLanguagePackageId: this.crossLanguagePackageId,
      APIForest: this.apiForest?.map(node => node.serialize()),
      Diagnostics: this.diagnostics.map(diag => diag.serialize())
    }
  }
}
