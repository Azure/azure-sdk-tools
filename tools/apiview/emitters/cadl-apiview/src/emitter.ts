// See: https://cadlwebsite.z1.web.core.windows.net/docs/extending-cadl/emitters-basics

import {
  emitFile,
  getServiceNamespace,
  getServiceTitle,
  getServiceVersion,
  NoTarget,
  Program,
  resolvePath,
} from "@cadl-lang/compiler";
import { buildVersionProjections } from "@cadl-lang/versioning";
import path from "path";
import mkdirp from "mkdirp";
import { ApiView } from "./apiview.js";
import { ApiViewEmitterOptions, reportDiagnostic } from "./lib.js";

const defaultOptions = {
  "output-file": "apiview.json",
} as const;

export interface ResolvedApiViewEmitterOptions {
  outputFile: string;
  serviceNamespace?: string;
}

export async function $onEmit(program: Program, emitterOptions?: ApiViewEmitterOptions) {
  const options = resolveOptions(program, emitterOptions ?? {});
  const emitter = createApiViewEmitter(program, options);
  await emitter.emitApiView();
}

export function resolveOptions(
  program: Program,
  options: ApiViewEmitterOptions
): ResolvedApiViewEmitterOptions {
  const resolvedOptions = { ...defaultOptions, ...options };

  return {
    outputFile: resolvePath(
      resolvedOptions["output-dir"] ?? `${program.compilerOptions.outputDir!}/apiview`,
      resolvedOptions["output-file"]
    ),
    serviceNamespace: resolvedOptions["service-namespace"]
  };
}

function createApiViewEmitter(program: Program, options: ResolvedApiViewEmitterOptions) {
  return { emitApiView };

  async function emitApiView() {
    const serviceNs = getServiceNamespace(program);
    if (!serviceNs) {
      throw new Error("No namespace found");
    }
    // TODO: Supply `version` option to select specific version
    let versionString = getServiceVersion(program);
    // TODO: Fix this wonky workaround when getServiceVersion is fixed.
    if (versionString == "0000-00-00") {
      versionString = undefined;
    }
    const namespaceString = options.serviceNamespace ?? program.checker.getNamespaceString(serviceNs);
    if (namespaceString == "") {
      reportDiagnostic(program, {
        code: "use-namespace-option",
        target: NoTarget
      });
    }
    const versions = buildVersionProjections(program, serviceNs);
    if (versions.length) {
      // TODO: Add heuristic to choose "latest" if not otherwise supplied
      // TODO: Validate that if version is supplied, it is found in the versioning list. Otherwise error.
    }
    // FIXME: Remove this weird workaround when this call is fixed.
    let serviceTitle = getServiceTitle(program);
    if (serviceTitle == "(title)") {
      serviceTitle = namespaceString;
    }
    const apiview = new ApiView(serviceTitle, namespaceString, versionString);
    apiview.emit(program);
    apiview.resolveMissingTypeReferences();

    if (!program.compilerOptions.noEmit && !program.hasError()) {
      const outputFolder = path.dirname(options.outputFile);
      try {
        await mkdirp(outputFolder);
      } catch {
        // mkdirp fails during tests
      }
      await emitFile(program, {
        path: options.outputFile,
        content: JSON.stringify(apiview.asApiViewDocument()) + "\n"
      });  
    }
  }
}
