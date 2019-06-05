/**
 * @fileoverview Linting rules for the JavaScript/TypeScript Azure SDK
 * @author Arpan Laha
 */
"use strict";

//------------------------------------------------------------------------------
// Requirements
//------------------------------------------------------------------------------

import requireIndex from "requireindex";
import { processors as JSONProcessors } from "eslint-plugin-json";

//------------------------------------------------------------------------------
// Plugin Definition
//------------------------------------------------------------------------------

// import all rules in lib/rules
module.exports.rules = requireIndex(__dirname + "/rules");

// import processors
module.exports.processors = {
  // add your processors here
  processors: JSONProcessors
};
