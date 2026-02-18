import { Path } from "../../../rustdoc-types/output/rustdoc-types";
import { PACKAGE_NAME } from "../../main";

export function replaceCratePath(path: string): string {
  return path.startsWith("crate::")
    ? path.replace("crate::", `${PACKAGE_NAME}::`) // replace "crate" with root mod name
    : path;
}

export function replaceSuperPrefix(path: string): string {
  return path.startsWith("super::") ? path.replace("super::", "") : path;
}

/**
 * Extracts the path e.g., `MyType` or `crate::MyType` from a `Path`.
 * 
 * Rustdoc format version 37 defined `Path::name` as a path.
 * Sometime between then and version 45 they renamed it more appropriately to `path`.
 */
interface PathV45 extends Path {
    /**
     * The `name` field was renamed `path` in format version 45.
     */
    path?: string,
}

export function getPath(path: PathV45): string {
    return path.path || path.name;
}
