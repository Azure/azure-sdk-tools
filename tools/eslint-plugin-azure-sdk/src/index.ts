/**
 * @fileoverview Linting rules for the JavaScript/TypeScript Azure SDK
 * @author Arpan Laha
 */

//------------------------------------------------------------------------------
// Requirements
//------------------------------------------------------------------------------

import rules from "./rules";
import processors from "./processors";
import configs from "./configs";

//------------------------------------------------------------------------------
// Plugin Definition
//------------------------------------------------------------------------------

export = { rules, processors, configs };
