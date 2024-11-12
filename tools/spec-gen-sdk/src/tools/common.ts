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
  