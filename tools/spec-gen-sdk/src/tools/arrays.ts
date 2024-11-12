/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

/**
 * Get whether or not the provided array contains any values.
 * @param values The array to check.
 * @returns Whether or not the provided array contains any values.
 */
export function any<T>(values: T[] | undefined): values is T[] {
    return !!(values && values.length > 0);
}
  

/**
 * Get all of the values within the provided array of values that match the provided condition.
 * @param values The array of values to filter.
 * @param condition The condition to look for within the array of values.
 * @returns The array of values from the original values that match the provided condition.
 */
export function where<T>(values: T[], condition: (value: T) => boolean): T[] {
    const result: T[] = [];
    for (const value of values) {
      if (condition(value)) {
        result.push(value);
      }
    }
    return result;
  }
  