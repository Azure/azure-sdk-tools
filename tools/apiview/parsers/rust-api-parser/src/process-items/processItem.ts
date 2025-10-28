import { ReviewLine } from "../models/apiview-models";
import { Item } from "../../rustdoc-types/output/rustdoc-types";
import { processConstant } from "./processConstant";
import { processEnum } from "./processEnum";
import { processFunction } from "./processFunction";
import { processModule } from "./processModule";
import { processStatic } from "./processStatic";
import { processStruct } from "./processStruct";
import { processStructField } from "./processStructField";
import { processTrait } from "./processTrait";
import { processUse } from "./processUse";
import { processUnion } from "./processUnion";
import { processTypeAlias } from "./processTypeAlias";
import { processTraitAlias } from "./processTraitAlias";
import { processExternType } from "./processExternType";
import { processMacro } from "./processMacro";
import { processProcMacro } from "./processProcMacro";
import { processAssocConst } from "./processAssocConst";
import { processAssocType } from "./processAssocType";

/**
 * Processes an item from the API JSON and returns a ReviewLine object.
 *
 * @param {Item} item - The item to process.
 * @param {object} parentModule - Optional parent module information
 * @param {string} lineIdPrefix - The prefix from ancestors for hierarchical LineId
 * @returns {ReviewLine | null} The ReviewLine object or null if the item is not processed.
 */
export function processItem(
  item: Item,
  parentModule?: { prefix: string; id: number },
  lineIdPrefix: string = "",
): ReviewLine[] | null {
  if (!item) {
    return null;
  }
  if (typeof item.inner === "object") {
    if ("module" in item.inner) {
      return processModule(item, parentModule, lineIdPrefix);
    } else if ("union" in item.inner) {
      return processUnion(item, lineIdPrefix);
    } else if ("struct" in item.inner) {
      return processStruct(item, lineIdPrefix);
    } else if ("enum" in item.inner) {
      return processEnum(item, lineIdPrefix);
    } else if ("function" in item.inner) {
      return processFunction(item, lineIdPrefix);
    } else if ("trait" in item.inner) {
      return processTrait(item, lineIdPrefix);
    } else if ("trait_alias" in item.inner) {
      return processTraitAlias(item, lineIdPrefix);
    } else if ("type_alias" in item.inner) {
      return processTypeAlias(item, lineIdPrefix);
    } else if ("constant" in item.inner) {
      return processConstant(item, lineIdPrefix);
    } else if ("static" in item.inner) {
      return processStatic(item, lineIdPrefix);
    } else if ("struct_field" in item.inner) {
      return [processStructField(item, lineIdPrefix)];
    } else if ("extern_type" in item.inner) {
      return processExternType(item, lineIdPrefix);
    } else if ("macro" in item.inner) {
      return processMacro(item, lineIdPrefix);
    } else if ("proc_macro" in item.inner) {
      return processProcMacro(item, lineIdPrefix);
    } else if ("assoc_const" in item.inner) {
      return processAssocConst(item, lineIdPrefix);
    } else if ("assoc_type" in item.inner) {
      return processAssocType(item, lineIdPrefix);
    }
  }
}
