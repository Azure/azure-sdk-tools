// See: https://cadlwebsite.z1.web.core.windows.net/docs/extending-cadl/emitters-basics

import {
  emitFile,
  getServiceNamespace,
  getServiceTitle,
  getServiceVersion,
  Namespace,
  Program,
  projectProgram,
  resolvePath,
} from "@cadl-lang/compiler";
import { buildVersionProjections } from "@cadl-lang/versioning";
import { ApiViewDocument } from "./apiview.js";
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

// TODO: Move this to Cadl compiler?
export function getFullyQualifiedNamespace(ns: Namespace, suffix?: string): string {
  suffix = suffix ?? ns.name;
  if (ns.namespace != undefined && ns.namespace.name != "") {
    return getFullyQualifiedNamespace(ns.namespace, `${ns.namespace.name}.${suffix}`);
  } else {
    return suffix;
  }
}

function createApiViewEmitter(program: Program, options: ResolvedApiViewEmitterOptions) {
  return { emitApiView };

  async function emitApiView() {
    const serviceNs = getServiceNamespace(program);
    if (!serviceNs) {
      return;
    }
    const versions = buildVersionProjections(program, serviceNs);
    for (const record of versions) {
      if (record.version) {
        record.projections.push({
          projectionName: "atVersion",
          arguments: [record.version],
        });
      }

      if (record.projections.length > 0) {
        program = projectProgram(program, record.projections);
      }

      await emitApiViewFromVersion(serviceNs, record.version);
    }
  }

  async function emitApiViewFromVersion(serviceNamespace: Namespace, version?: string) {
    const rootNamespaceName = getFullyQualifiedNamespace(serviceNamespace);
    const serviceTitle = getServiceTitle(program);
    const serviceVersion = version ?? getServiceVersion(program);
    const apiview = new ApiViewDocument(serviceTitle, rootNamespaceName, serviceVersion);
    apiview.emit(program);
    apiview.resolveMissingTypeReferences();

    const tokenJson = JSON.stringify(apiview) + "\n";
    await emitFile(program, {
      path: options["output-file"],
      content: tokenJson,
    });
  }
}
