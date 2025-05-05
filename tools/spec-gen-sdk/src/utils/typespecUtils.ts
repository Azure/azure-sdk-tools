import YAML from "yaml";
import { SpecConfig } from '../types/SpecConfig';
import { configError } from "./messageUtils";

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
    yamlContent = YAML.parse(content) as YamlContent;
  } catch (error) {
    throw new Error(configError(`The parsing of the file was unsuccessful. Please fix the 'tspconfig.yaml' file. Error Details: ${error.stack}`));
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
