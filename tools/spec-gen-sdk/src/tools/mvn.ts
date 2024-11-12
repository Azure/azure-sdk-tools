/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

import * as os from "os";

/**
 * Options that can be passed to mvnExecutable().
 */
export interface MvnExecutableOptions {
  /**
   * The platform that AutoRest will be run on.
   */
  osPlatform?: string;
  /**
   * The path to the AutoRest executable. This can be either the folder that the executable is in or
   * the path to the executable itself.
   */
  mvnPath?: string;
}

/**
 * Get the executable that will be used to run mvn (Maven).
 * @param options The options for specifying which executable to use.
 */
export function mvnExecutable(options: MvnExecutableOptions = {}): string {
  if (!options.osPlatform) {
    options.osPlatform = os.platform();
  }
  let result: string = options.mvnPath || "";
  if (!result.endsWith("mvn") && !result.endsWith("mvn.cmd")) {
    if (result && !result.endsWith("/") && !result.endsWith("\\")) {
      result += "/";
    }
    result += "mvn";
  }
  if (options.osPlatform === "win32" && !result.endsWith(".cmd")) {
    result += ".cmd";
  }
  return result;
}
