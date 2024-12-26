import * as jsYaml from "js-yaml";

/**
 * The parsed version of the swagger-to-sdk YAML block within an AutoRest readme.md file.
 */
export interface ReadmeMdSwaggerToSDKConfiguration {
  /**
   * The repositories specified.
   */
  repositories: RepositoryConfiguration[];
}

/**
 * An individual repository configuration within an AutoRest readme.md swagger-to-sdk YAML block
 * configuration.
 */
export interface RepositoryConfiguration {
  /**
   * The name of the GitHub repository this configuration applies to. If no organization is
   * specified, then Azure will be used.
   */
  repo: string;
  /**
   * The list of commands that will be run (in order) after an SDK has been generated.
   */
  after_scripts: string[];
}

export function findSwaggerToSDKBlocks(parseMarkdown: string):{ info: string; content: string;}[] {
  const codeBlockRegex = /```(.*?)\n([\s\S]*?)```/g;
  const codeBlocks: { info: string; content: string }[] = [];
  let match: null | RegExpExecArray;
  
  while ((match = codeBlockRegex.exec(parseMarkdown)) !== null) {
    const info = match[1].trim();
    const content = match[2].trim();
    codeBlocks.push({ info, content });
  }

  return codeBlocks;
 }

/**
 * Parse the contents of an AutoRest readme.md configuration file and return the parsed swagger to
 * sdk configuration section.
 * @param readmeMdFileContents The contents of an AutoRest readme.md configuration file.
 */
export function findSwaggerToSDKConfiguration(readmeMdFileContents: string | undefined): ReadmeMdSwaggerToSDKConfiguration | undefined {
  let result: ReadmeMdSwaggerToSDKConfiguration | undefined;
  if (readmeMdFileContents) {
    const swaggerToSDKBlocks:{ info: string; content: string;}[] = findSwaggerToSDKBlocks(readmeMdFileContents);
    const swaggerToSDKYamlBlocks = swaggerToSDKBlocks.filter((block) => block.info.toLowerCase().indexOf("$(swagger-to-sdk)") !== -1);
    const repositories: RepositoryConfiguration[] = [];
    for (const swaggerToSDKYamlBlock of swaggerToSDKYamlBlocks) {
      const yamlBlockContents: string | null = swaggerToSDKYamlBlock.content;
      if (yamlBlockContents) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const yaml: any = jsYaml.safeLoad(yamlBlockContents);
        if (yaml) {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const swaggerToSDK: any = yaml["swagger-to-sdk"];
          if (swaggerToSDK && Array.isArray(swaggerToSDK)) {
            repositories.push(...swaggerToSDK);
          }
        }
      }  
    } 
    result = { repositories };
  }
  return result;
}
