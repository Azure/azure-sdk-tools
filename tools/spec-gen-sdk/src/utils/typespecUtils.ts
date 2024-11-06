import { load } from 'js-yaml';
import { SpecConfig } from '../types/SpecConfig';
import { join } from "path";
import { readFileSync } from 'fs';
import { WorkflowContext } from '../automation/workflow';
import { parseYamlContent } from '@azure/swagger-validation-common';

export const findSDKToGenerateFromTypeSpecProject = (content: string | undefined, specConfig: SpecConfig) => {
  if (!specConfig?.typespecEmitterToSdkRepositoryMapping || !content) {
    return [];
  }
  interface YamlContent {
    options?: Record<string, unknown>;
    emitters?: Record<string, unknown>;
  }
  let yamlContent: YamlContent;
  try {
    yamlContent = load(content) as YamlContent;
  } catch (error) {
    throw new Error(`The parsing of the file was unsuccessful. Please make the necessary corrections to the 'tspconfig.yaml' file! Error Details: ${error.stack}`);
  }
  const emitters = new Set<string>();
  const emittersInYaml = yamlContent.options ?? yamlContent.emitters;
  if (emittersInYaml) {
    for (const emitter of Object.keys(emittersInYaml)) {
      if (!emittersInYaml[emitter]) {
        continue;
      }
      emitters.add(emitter);
    }
  }
  return [...emitters].filter(e => !!specConfig.typespecEmitterToSdkRepositoryMapping[e])
    .map(e => specConfig.typespecEmitterToSdkRepositoryMapping[e]);
};

export function getTypeSpecProjectServiceName(outputFolder: string): string {
  const match = outputFolder.match(/[^\/]*\/([^\/]*)\//);
  return match ? match[1] : '';
}

export function getTypeSpecProjectResourceProvider(typespecProject: string): string {
  const match = typespecProject.match(/[^\/]*\/([^\/]*)\/(.*)/);
  return match ? match[2] : '';
}

export function getTypeSpecOutputFolder(typespecProject: string, context: WorkflowContext) {
  const projectInfo = getTypeSpecProjectInfo(typespecProject, context);
  const TypeSpecProjectServiceName = getTypeSpecProjectServiceName(projectInfo.outputFolder);
  if (TypeSpecProjectServiceName === "{service-name}") {
    return `${projectInfo.resourceProviderFolder}/${getTypeSpecProjectResourceProvider(typespecProject)}`;
  } else {
    return `${projectInfo.resourceProviderFolder}/${TypeSpecProjectServiceName}`;
  }
}

export function getTypeSpecProjectInfo(typespecProject: string, context: WorkflowContext) {
  const typespecProjectYamlFile = join(context.specFolder, typespecProject, "tspconfig.yaml");
  let typespecProjectYaml: string = '';
  try {
    typespecProjectYaml = readFileSync(typespecProjectYamlFile).toString();
  } catch (error) {
    context.logger.error(`IOError: Fails to read tspconfig.yaml file with path of '${typespecProjectYamlFile}'. Ensure the file exist then re-run the pipeline. If the issue persists, report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    throw error;
  }
  const typespecProjectContent = parseYamlContent(typespecProjectYaml, typespecProjectYamlFile);;
  const typespecAutorestEmitterConfig = typespecProjectContent.result?.["options"]?.["@azure-tools/typespec-autorest"];

  return {
    outputFolder: typespecAutorestEmitterConfig?.["output-file"],
    resourceProviderFolder: typespecAutorestEmitterConfig?.["azure-resource-provider-folder"],
  };
}
