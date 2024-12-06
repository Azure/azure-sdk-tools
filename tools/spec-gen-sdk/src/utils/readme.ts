import * as commonmark from "commonmark";
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

function findSwaggerToSDKYamlBlocks(parsedMarkdownNode: commonmark.Node | undefined | null): commonmark.Node[] {
  const result: commonmark.Node[] = [];
  if (parsedMarkdownNode) {
    const nodesToVisit: commonmark.Node[] = [parsedMarkdownNode];
    while (nodesToVisit.length > 0) {
      const node: commonmark.Node = nodesToVisit.shift()!;

      if (node.firstChild) {
        nodesToVisit.push(node.firstChild);
      }
      if (node.next) {
        nodesToVisit.push(node.next);
      }

      if (node.type === "code_block" && node.info && node.info.toLowerCase().indexOf("$(swagger-to-sdk)") !== -1) {
        result.push(node);
      }
    }
  }
  return result;
}

/**
 * Parse the contents of an AutoRest readme.md configuration file and return the parsed swagger to
 * sdk configuration section.
 * @param readmeMdFileContents The contents of an AutoRest readme.md configuration file.
 */
export function findSwaggerToSDKConfiguration(readmeMdFileContents: string | undefined): ReadmeMdSwaggerToSDKConfiguration | undefined {
  let result: ReadmeMdSwaggerToSDKConfiguration | undefined;
  if (readmeMdFileContents) {
    const markdownParser = new commonmark.Parser();
    const parsedReadmeMd: commonmark.Node = markdownParser.parse(readmeMdFileContents);
    const swaggerToSDKYamlBlocks: commonmark.Node[] = findSwaggerToSDKYamlBlocks(parsedReadmeMd);
    const repositories: RepositoryConfiguration[] = [];
    for (const swaggerToSDKYamlBlock of swaggerToSDKYamlBlocks) {
      const yamlBlockContents: string | null = swaggerToSDKYamlBlock.literal;
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
