/**
 * @fileoverview All rules
 * @author Arpan Laha
 */

import { rule as tsConfigAllowSyntheticDefaultImports } from "./ts-config-allowsyntheticdefaultimports";
import { rule as tsConfigDeclaration } from "./ts-config-declaration";
import { rule as tsConfigStrict } from "./ts-config-strict";

export const rules = {
  "ts-config-allowsyntheticdefaultimports": tsConfigAllowSyntheticDefaultImports,
  "ts-config-declaration": tsConfigDeclaration,
  "ts-config-strict": tsConfigStrict
};
