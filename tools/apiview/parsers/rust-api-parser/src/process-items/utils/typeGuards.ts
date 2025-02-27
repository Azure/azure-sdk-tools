import { Item, Impl, Struct, Union, Enum, Trait, Static, Module, Function, Constant, Type, Use, ItemEnum } from "../../../rustdoc-types/output/rustdoc-types";

export function isUseItem(item: Item): item is Item & { inner: { use: Use } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'use' in item.inner;
}

export function isImplItem(item: Item): item is Item & { inner: { impl: Impl } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'impl' in item.inner;
}
export function isStructItem(item: Item): item is Item & { inner: { struct: Struct } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'struct' in item.inner;
}
export function isUnionItem(item: Item): item is Item & { inner: { union: Union } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'union' in item.inner;
}
export function isEnumItem(item: Item): item is Item & { inner: { enum: Enum } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'enum' in item.inner;
}
export function isTraitItem(item: Item): item is Item & { inner: { trait: Trait } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'trait' in item.inner;
}
export function isStaticItem(item: Item): item is Item & { inner: { static: Static } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'static' in item.inner;
}
export function isModuleItem(item: Item): item is Item & { inner: { module: Module } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'module' in item.inner;
}
export function isFunctionItem(item: Item): item is Item & { inner: { function: Function } } {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'function' in item.inner;
}
export function isConstantItem(item: Item): item is Item & {
  inner: {
    "constant": {
      type: Type;
      const: Constant;
    }
  }
} {
  return item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && 'constant' in item.inner;
}