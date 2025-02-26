import { ReviewLine } from "../models/apiview-models";
import { Crate, Item } from "../../rustdoc-types/output/rustdoc-types";
import { processConstant } from "./processConstant";
import { processEnum } from "./processEnum";
import { processFunction } from "./processFunction";
import { processModule } from "./processModule";
import { processStatic } from "./processStatic";
import { processStruct } from "./processStruct";
import { processTrait } from "./processTrait";
import { processUse } from "./processUse";
import { processUnion } from "./processUnion";

/**
 * Processes an item from the API JSON and returns a ReviewLine object.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The item to process.
 * @returns {ReviewLine | null} The ReviewLine object or null if the item is not processed.
 */
export function processItem(item: Item, apiJson: Crate): ReviewLine[] | null {
  if (typeof item.inner === "object") {
    if ("module" in item.inner) {
      return processModule(apiJson, item);
    } else if ("use" in item.inner) {
      return processUse(item);
    } else if ("union" in item.inner) {
      return processUnion(item, apiJson);
    } else if ("struct" in item.inner) {
      return processStruct(item, apiJson);
    } else if ("enum" in item.inner) {
      return processEnum(item, apiJson);
    } else if ("function" in item.inner) {
      return processFunction(item);
    } else if ("trait" in item.inner) {
      return processTrait(item, apiJson);
    } else if ("trait_alias" in item.inner) {
      return null;
    } else if ("type_alias" in item.inner) {
      return null;
    } else if ("constant" in item.inner) {
      return processConstant(item);
    } else if ("static" in item.inner) {
      return processStatic(item);
    } else if ("extern_type" in item.inner) {
      return null;
    } else if ("macro" in item.inner) {
      return null;
    } else if ("proc_macro" in item.inner) {
      return null;
    } else if ("assoc_const" in item.inner) {
      return null;
    } else if ("assoc_type" in item.inner) {
      return null;
    }
  }
}
