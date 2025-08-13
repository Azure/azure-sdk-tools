import _ from "lodash";
import YAML from "yaml";

export function diffStringArrays(left: string[], right: string[]): { diffResult: string[], hasDiff: boolean} {
  const unchanged = right.filter(item => left.includes(item));
  const added = right.filter(item => !left.includes(item));
  const diffResult: string[] = [];

  unchanged.forEach(item => {
    diffResult.push(`\t${item}`);
  });

  added.forEach(item => {
    diffResult.push(`+\t${item}`);
  });

  return {
    diffResult,
    hasDiff: added.length > 0
  };
  
}

export function getValueByKey<T>(
    arrayOfObjects: { [key: string]: T }[],
    targetKey: string
): T | undefined {
    const foundObject = _.find(arrayOfObjects, targetKey);
    return foundObject ? foundObject[targetKey] : undefined;
}

export function removeAnsiEscapeCodes<T extends string | string[]>(messages: T): T {
    // eslint-disable-next-line no-control-regex
    const ansiEscapeCodeRegex = /\x1b\[(\d{1,2}(;\d{0,2})*)?[A-HJKSTfimnsu]/g;
    if (typeof messages === "string") {
        return messages.replace(ansiEscapeCodeRegex, "") as T;
    }
    return messages.map((item) => item.replace(ansiEscapeCodeRegex, "")) as T;
}

/**
 * Parse YAML content into an object.
 * @param {string} yamlContent The YAML content to parse.
 * @returns {string | object | null} The parsed YAML content or null if empty.
 * @throws {Error} Throws an error if parsing fails.
 */
export function parseYamlContent(
  yamlContent: string,
): string | object | null {
  // Parse the YAML content - will throw an exception if invalid
  const content = YAML.parse(yamlContent);

  // If yaml file is empty, return null to distinguish from parsing errors
  if (!content) {
    return null;
  }

  return content;
}

/**
 * Replace all of the instances of searchValue in value with the provided replaceValue.
 * @param {string | undefined} value The value to search and replace in.
 * @param {string} searchValue The value to search for in the value argument.
 * @param {string} replaceValue The value to replace searchValue with in the value argument.
 * @returns {string | undefined} The value where each instance of searchValue was replaced with replacedValue.
 */
export function replaceAll(value: string | undefined, searchValue: string, replaceValue: string): string | undefined {
  return !value || !searchValue ? value : value.split(searchValue).join(replaceValue || "");
}

/**
 * Extract and format the prefix from tspConfigPath or readmePath.
 * @param {string | undefined} tspConfigPath The tspConfigPath to extract the prefix from.
 * @param {string | undefined} readmePath The readmePath to extract the prefix from.
 * @returns {string} The formatted prefix.
 */
export function extractPathFromSpecConfig(tspConfigPath: string | undefined, readmePath: string | undefined): string {
  let prefix = '';
  if (tspConfigPath) {
    const match = tspConfigPath.match(/specification\/(.+)\/tspconfig\.yaml$/);
    if (match) {
      const segments = match[1].split('/');
      prefix = segments.join('-').toLowerCase().replace(/\./g, '-');
    }
  } else if (readmePath) {
    const match = readmePath.match(/specification\/(.+?)\/readme\.md$/i);
    if (match) {
      const segments = match[1].split('/');
      prefix = segments.join('-').toLowerCase().replace(/\./g, '-');
    }
  }
  return prefix ? prefix : `no-readme-tspconfig-${Math.floor(Math.random() * 1000)}`;
}

/**
 * Convert a Map to an object.
 * @param {Map<K, V>} map The Map to convert to an object.
 * @returns {Record<string, V>} The object representation of the Map.
 */
export function mapToObject<K, V>(map: Map<K, V>): Record<string, V> {
  const obj: Record<string, V> = {};
  for (const [key, value] of map.entries()) {
    obj[String(key)] = value;
  }
  return obj;
}
