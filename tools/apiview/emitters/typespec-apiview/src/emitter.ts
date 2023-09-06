import {
  EmitContext,
  emitFile,
  getNamespaceFullName,
  listServices,
  Namespace,
  NoTarget,
  Program,
  ProjectionApplication,
  projectProgram,
  resolvePath,
  Service,
} from "@typespec/compiler";
import { buildVersionProjections, getVersion } from "@typespec/versioning";
import path from "path";
import { ApiView } from "./apiview.js";
import { ApiViewEmitterOptions, reportDiagnostic } from "./lib.js";

export interface ResolvedApiViewEmitterOptions {
  emitterOutputDir: string;
  outputFile?: string;
  service?: string;
  version?: string;
  includeGlobalNamespace: boolean;
}

export async function $onEmit(context: EmitContext<ApiViewEmitterOptions>) {
  const options = resolveOptions(context);
  const emitter = createApiViewEmitter(context.program, options);
  await emitter.emitApiView();
}

export function resolveOptions(context: EmitContext<ApiViewEmitterOptions>): ResolvedApiViewEmitterOptions {
  const resolvedOptions = { ...context.options };

  return {
    emitterOutputDir: context.emitterOutputDir,
    outputFile: resolvedOptions["output-file"],
    service: resolvedOptions["service"],
    version: resolvedOptions["version"],
    includeGlobalNamespace: resolvedOptions["include-global-namespace"] ?? false,
  };
}

function resolveNamespaceString(namespace: Namespace): string | undefined {
  // FIXME: Fix this wonky workaround when getNamespaceString is fixed.
  const value = getNamespaceFullName(namespace);
  return value === "" ? undefined : value;
}

// TODO: Up-level this logic?
function resolveAllowedVersions(program: Program, service: Service): string[] {  
  const allowed: string[] = [];
  const serviceVersion = service.version;
  const versions = getVersion(program, service.type)?.getVersions();
  if (serviceVersion !== undefined && versions !== undefined) {
    throw new Error("Cannot have serviceVersion with multi-API.");
  }
  if (serviceVersion !== undefined) {
    allowed.push(serviceVersion);
  } else if (versions !== undefined) {
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
    return versions.filter((item) => item.name === version).map((item) => item.value)[0];
  } catch {
    return version;
  }
}

function resolveProgramForVersion(program: Program, namespace: Namespace, versionKey?: string): Program {
  if (!versionKey) {
    return program;
  }
  const version = resolveVersionValue(program, namespace, versionKey);
  const projections = buildVersionProjections(program, namespace).filter((item) => item.version === version);
  if (projections.length === 0) {
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

/**
 * Ensures that single-value options are not used in multi-service specs unless the
 * `--service` option is specified. Single-service specs need not pass this option.
 */
function validateMultiServiceOptions(program: Program, services: Service[], options: ResolvedApiViewEmitterOptions) {
  for (const [name, val] of [["output-file", options.outputFile], ["version", options.version]]) {
    if (val && !options.service && services.length > 1) {
      reportDiagnostic(program, {
        code: "invalid-option",
        target: NoTarget,
        format: {
          name: name!
        }
      })
    }
  }
}

/**
 * If the `--service` option is provided, ensures the service exists and returns the filtered list.
 */
function applyServiceFilter(program: Program, services: Service[], options: ResolvedApiViewEmitterOptions): Service[] {
  if (!options.service) {
    return services;
  }
  const filtered = services.filter( (x) => x.title === options.service);
  if (!filtered.length) {
    reportDiagnostic(program, {
      code: "invalid-service",
      target: NoTarget,
      format: {
        value: options.service
      }
    });
  }
  return filtered;
}

function createApiViewEmitter(program: Program, options: ResolvedApiViewEmitterOptions) {
  return { emitApiView };

  async function emitApiView() {
    let services = listServices(program);
    if (!services.length) {
      reportDiagnostic(program, {
        code: "no-services-found",
        target: NoTarget
      })
      return;
    }
    // applies the default "apiview.json" filename if not provided and there's only a single service
    if (services.length === 1) {
      options.outputFile = options.outputFile ?? "apiview.json"
    }
    validateMultiServiceOptions(program, services, options);
    services = applyServiceFilter(program, services, options);

    for (const service of services) {
      const versionString = options.version ?? service.version;
      const namespaceString = resolveNamespaceString(service.type) ?? "Unknown"
      const serviceTitle = service.title ? service.title : namespaceString;
      const allowedVersions = resolveAllowedVersions(program, service);
      if (versionString) {
        if (allowedVersions.filter((version) => version === versionString).length === 0) {
          reportDiagnostic(program, {
            code: "version-not-found",
            target: NoTarget,
            format: {
              version: versionString,
              serviceTitle: serviceTitle,
              allowed: allowedVersions.join(" | "),
            },
          })
          return;
        }  
      }      
      const resolvedProgram = resolveProgramForVersion(program, service.type, versionString);
  
      const apiview = new ApiView(serviceTitle, namespaceString, versionString, options.includeGlobalNamespace);
      apiview.emit(resolvedProgram);
      apiview.resolveMissingTypeReferences();

      if (!program.compilerOptions.noEmit && !program.hasError()) {
        const outputFolder = path.dirname(options.emitterOutputDir);
        await program.host.mkdirp(outputFolder);
        const outputFile = options.outputFile ?? `${namespaceString}-apiview.json`;
        const outputPath = resolvePath(outputFolder, outputFile);
        await emitFile(program, {
          path: outputPath,
          content: JSON.stringify(apiview.asApiViewDocument()) + "\n"
        });  
      }    
    }
  }
}
