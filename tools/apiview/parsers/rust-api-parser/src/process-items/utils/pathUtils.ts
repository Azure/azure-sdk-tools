import { PACKAGE_NAME } from "../../main";

export function replaceCratePath(path: string): string {
  return path.startsWith("crate::")
    ? path.replace("crate::", `${PACKAGE_NAME}::`) // replace "crate" with root mod name
    : path;
}

export function replaceSuperPrefix(path: string): string {
  return path.startsWith("super::") ? path.replace("super::", "") : path;
}
