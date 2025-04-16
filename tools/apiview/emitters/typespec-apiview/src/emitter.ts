import {
  EmitContext,
  emitFile,
  getNamespaceFullName,
  listServices,
  Namespace,
  NoTarget,
  Program,
  resolvePath,
  Service,
} from "@typespec/compiler";
import path from "path";
import { ApiView } from "./apiview.js";
import { ApiViewEmitterOptions, reportDiagnostic } from "./lib.js";

export interface ResolvedApiViewEmitterOptions {
  emitterOutputDir: string;
  outputFile?: string;
  service?: string;
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
    includeGlobalNamespace: resolvedOptions["include-global-namespace"] ?? false,
  };
}

function resolveNamespaceString(namespace: Namespace): string | undefined {
  // FIXME: Fix this wonky workaround when getNamespaceString is fixed.
  const value = getNamespaceFullName(namespace);
  return value === "" ? undefined : value;
}

/**
 * Ensures that single-value options are not used in multi-service specs unless the
 * `--service` option is specified. Single-service specs need not pass this option.
 */
function validateMultiServiceOptions(program: Program, services: Service[], options: ResolvedApiViewEmitterOptions) {
  for (const [name, val] of [["output-file", options.outputFile]]) {
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
      const namespaceString = resolveNamespaceString(service.type) ?? "Unknown"
      const serviceTitle = service.title ? service.title : namespaceString;
      
      const apiview = new ApiView(serviceTitle, namespaceString, options.includeGlobalNamespace);
      apiview.compile(program);
      apiview.resolveMissingTypeReferences();

      if (!program.compilerOptions.noEmit && !program.hasError()) {
        const outputFolder = path.dirname(options.emitterOutputDir);
        await program.host.mkdirp(outputFolder);
        const outputFile = options.outputFile ?? `${namespaceString}-apiview.json`;
        const outputPath = resolvePath(outputFolder, outputFile);
        await emitFile(program, {
          path: outputPath,
          content: JSON.stringify(apiview.asCodeFile()) + "\n"
        });  
      }    
    }
  }
}
