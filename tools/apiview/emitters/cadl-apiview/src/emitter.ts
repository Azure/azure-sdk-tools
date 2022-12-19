// See: https://cadlwebsite.z1.web.core.windows.net/docs/extending-cadl/emitters-basics

import {
  EmitContext,
  emitFile,
  getServiceNamespace,
  getServiceTitle,
  getServiceVersion,
  Namespace,
  NoTarget,
  Program,
  ProjectionApplication,
  projectProgram,
  resolvePath,
} from "@cadl-lang/compiler";
import { buildVersionProjections, getVersion } from "@cadl-lang/versioning";
import path from "path";
import { ApiView } from "./apiview.js";
import { ApiViewEmitterOptions, reportDiagnostic } from "./lib.js";

const defaultOptions = {
  "output-file": "apiview.json",
} as const;

export interface ResolvedApiViewEmitterOptions {
  emitterOutputDir?: string;
  outputPath: string;
  namespace?: string;
  version?: string;
}

export async function $onEmit(context: EmitContext<ApiViewEmitterOptions>) {
  const options = resolveOptions(context);
  const emitter = createApiViewEmitter(context.program, options);
  await emitter.emitApiView();
}

export function resolveOptions(context: EmitContext<ApiViewEmitterOptions>): ResolvedApiViewEmitterOptions {
  const resolvedOptions = { ...defaultOptions, ...context.options };

  return {
    outputPath: resolvePath(
      context.emitterOutputDir,
      resolvedOptions["output-file"]
    ),
    namespace: resolvedOptions["namespace"],
    version: resolvedOptions["version"],
  };
}

function resolveServiceVersion(program: Program): string | undefined {
  // FIXME: Fix this wonky workaround when getServiceVersion is fixed.
  const value = getServiceVersion(program);
  return value == "0000-00-00" ? undefined : value;
}

function resolveNamespaceString(program: Program, namespace: Namespace): string | undefined {
  // FIXME: Fix this wonky workaround when getNamespaceString is fixed.
  const value = program.checker.getNamespaceString(namespace);
  return value == "" ? undefined : value;
}

// TODO: Up-level this logic?
function resolveAllowedVersions(program: Program, namespace: Namespace): string[] {  
  const allowed: string[] = [];
  const serviceVersion = resolveServiceVersion(program);
  const versions = getVersion(program, namespace)?.getVersions();
  if (serviceVersion != undefined && versions != undefined) {
    throw new Error("Cannot have serviceVersion with multi-API.");
  }
  if (serviceVersion != undefined) {
    allowed.push(serviceVersion);
  } else if (versions != undefined) {
    for (const item of versions) {
      allowed.push(item.name);
    }
  } else {
    throw new Error("Unable to resolve allowed versions.");
  }
  return allowed;
}

function resolveVersionValue(program: Program, namespace: Namespace, version: string): string {
  try {
    const versions = getVersion(program, namespace)!.getVersions();
    return versions.filter((item) => item.name == version).map((item) => item.value)[0];
  } catch {
    return version;
  }
}

function resolveProgramForVersion(program: Program, namespace: Namespace, versionKey?: string): Program {
  if (!versionKey) {
    return program;
  }
  const version = resolveVersionValue(program, namespace, versionKey);
  const projections = buildVersionProjections(program, namespace).filter((item) => item.version == version);
  if (projections.length == 0) {
    // non-multi-version scenario. Return original program.
    return program;
  } else {
    // alias could result in an api version appearing twice, so always take the first
    const projection = projections[0];
    projection.projections.push({
      projectionName: "atVersion",
      arguments: [version],
    });
    const commonProjections: ProjectionApplication[] = [
      {
        projectionName: "target",
        arguments: ["json"],
      },
    ];
    return projectProgram(program, [...commonProjections, ...projection.projections]);
  }
}

function createApiViewEmitter(program: Program, options: ResolvedApiViewEmitterOptions) {
  return { emitApiView };

  async function emitApiView() {
    const serviceNs = getServiceNamespace(program);
    if (!serviceNs) {
      throw new Error("No namespace found");
    }
    const versionString = options.version ?? resolveServiceVersion(program);
    const namespaceString = options.namespace ?? resolveNamespaceString(program, serviceNs);
    if (namespaceString == undefined) {
      reportDiagnostic(program, {
        code: "use-namespace-option",
        target: NoTarget
      });
      return;
    }
    const allowedVersions = resolveAllowedVersions(program, serviceNs);
    if (versionString) {
      if (allowedVersions.filter((version) => version == versionString).length == 0) {
        reportDiagnostic(program, {
          code: "version-not-found",
          target: NoTarget,
          format: {
            version: versionString,
            allowed: allowedVersions.join(" | "),
          },
        })
        return;
      }  
    }
    // FIXME: Fix this wonky workaround when getServiceTitle is fixed.
    let serviceTitle = getServiceTitle(program);
    if (serviceTitle == "(title)") {
      serviceTitle = namespaceString;
    }
    const resolvedProgram = resolveProgramForVersion(program, serviceNs, versionString);
    const apiview = new ApiView(serviceTitle, namespaceString, versionString);
    apiview.emit(resolvedProgram);
    apiview.resolveMissingTypeReferences();

    if (!program.compilerOptions.noEmit && !program.hasError()) {
      const outputFolder = path.dirname(options.outputPath);
      await program.host.mkdirp(outputFolder);
      await emitFile(program, {
        path: options.outputPath,
        content: JSON.stringify(apiview.asApiViewDocument()) + "\n"
      });  
    }
  }
}
