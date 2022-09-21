// See: https://cadlwebsite.z1.web.core.windows.net/docs/extending-cadl/emitters-basics

import {
  emitFile,
  getServiceNamespace,
  getServiceTitle,
  getServiceVersion,
  Program,
  resolvePath,
} from "@cadl-lang/compiler";
import { ApiView } from "./apiview.js";
import { ApiViewEmitterOptions } from "./lib.js";

const defaultOptions = {
  "output-file": "apiview.json",
} as const;

export interface ResolvedApiViewEmitterOptions {
  "output-file": string;
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
    "output-file": resolvePath(
      program.compilerOptions.outputPath ?? "./output",
      resolvedOptions["output-file"]
    ),
  };
}

function createApiViewEmitter(program: Program, options: ResolvedApiViewEmitterOptions) {
  return { emitApiView };

  async function emitApiView() {
    const serviceNs = getServiceNamespace(program);
    if (!serviceNs) {
      return;
    }
    const versionString = "TODO";
    await emitApiViewFromVersion(program.checker.getNamespaceString(serviceNs), versionString);
  }

  async function emitApiViewFromVersion(namespaceString: string, version?: string) {
    const serviceTitle = getServiceTitle(program);
    const serviceVersion = version ?? getServiceVersion(program);
    const apiview = new ApiView(serviceTitle, namespaceString, serviceVersion);
    apiview.emit(program);
    apiview.resolveMissingTypeReferences();

    const tokenJson = JSON.stringify(apiview.asApiViewDocument()) + "\n";
    await emitFile(program, {
      path: options["output-file"],
      content: tokenJson,
    });
  }
}
