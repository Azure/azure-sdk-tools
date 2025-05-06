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
import { createSdkContext } from "@azure-tools/typespec-client-generator-core";
import path from "path";
import { ApiView, PackageData } from "./apiview.js";
import { ApiViewEmitterOptions, reportDiagnostic } from "./lib.js";

export interface ResolvedApiViewEmitterOptions {
  emitterOutputDir: string;
  outputFile?: string;
  service?: string;
  includeGlobalNamespace: boolean;
}

interface SdkEmitterOptions {
  namespace?: string;
}

async function getPackageData(context: EmitContext<ApiViewEmitterOptions>): Promise<Map<string, PackageData>> {
  const emitterNames = ["@azure-tools/typespec-csharp", "@azure-tools/typespec-java", "@azure-tools/typespec-python", "@azure-tools/typespec-ts"];
  const packageNamespaces = new Map<string, PackageData>();
  const originalOptions = context.options;
  for (const emitterName of emitterNames) {
    const packageOptions = context.program.compilerOptions.options![emitterName] as SdkEmitterOptions;
    context.options = packageOptions as unknown as ApiViewEmitterOptions;
    const sdkContext = await createSdkContext(context, emitterName);
    const namespace = sdkContext.sdkPackage.clients[0]?.namespace;
    const data: PackageData = {
      namespace: namespace ?? "Unknown",
      packageName: "Unknown",
    }
    packageNamespaces.set(emitterName, data);
  }
  context.options = originalOptions;
  return packageNamespaces;
}

export async function $onEmit(context: EmitContext<ApiViewEmitterOptions>) {
  const options = resolveOptions(context);
  const packageNamespaces = await getPackageData(context);
  const emitter = createApiViewEmitter(context.program, packageNamespaces, options);
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

function createApiViewEmitter(program: Program, packageNamespaces: Map<string, PackageData>, options: ResolvedApiViewEmitterOptions) {
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

      const apiview = new ApiView(serviceTitle, namespaceString, packageNamespaces, options.includeGlobalNamespace);
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
