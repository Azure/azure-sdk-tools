import { PACKAGE_NAME } from "../../main";

export function replaceCratePath(path: string): string {
    return path.startsWith("crate::") 
        ? path.replace("crate::", `${PACKAGE_NAME}::`) // replace "crate" with root mod name
        : path;
}
