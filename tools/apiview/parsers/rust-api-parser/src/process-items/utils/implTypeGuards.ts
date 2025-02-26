import { ImplInner, TypedItem } from "./typeGuards";

export type ImplItem = TypedItem<ImplInner>;

export function isAutoDerivedImpl(implItem: ImplItem): boolean {
  return (
    implItem.inner.impl.blanket_impl === null &&
    implItem.inner.impl.trait !== null &&
    implItem.attrs.includes("#[automatically_derived]")
  );
}

export function isManualTraitImpl(implItem: ImplItem): boolean {
  return (
    implItem.inner.impl.blanket_impl === null &&
    implItem.inner.impl.trait !== null &&
    !implItem.attrs.includes("#[automatically_derived]")
  );
}

export function isInherentImpl(implItem: ImplItem): boolean {
  return (
    implItem.inner.impl.blanket_impl === null &&
    implItem.inner.impl.trait === null
  );
}

export function getImplsFromItem(item: { inner: { [key: string]: { impls: number[] } } }): number[] {
  if ("struct" in item.inner) return item.inner.struct.impls;
  if ("union" in item.inner) return item.inner.union.impls;
  return item.inner.enum.impls;
}
