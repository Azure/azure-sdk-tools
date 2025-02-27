import { Item, Impl, Struct, Union, Enum, Trait, Static, Module, Function, Constant, Type, Use } from "../../../rustdoc-types/output/rustdoc-types";

type ItemTypes = {
  use: Use;
  impl: Impl;
  struct: Struct;
  union: Union;
  enum: Enum;
  trait: Trait;
  static: Static;
  module: Module;
  function: Function;
  constant: { type: Type; const: Constant };
};

type ItemTypeGuard<K extends keyof ItemTypes> = 
  (item: Item) => item is Item & { inner: { [P in K]: ItemTypes[K] } };

const createTypeGuard = <K extends keyof ItemTypes>(type: K): ItemTypeGuard<K> =>
  ((item: Item): item is Item & { inner: { [P in K]: ItemTypes[K] } } =>
    item && typeof item === 'object' && item.inner && typeof item.inner === 'object' && type in item.inner);

export const isUseItem = createTypeGuard('use');
export const isImplItem = createTypeGuard('impl');
export const isStructItem = createTypeGuard('struct');
export const isUnionItem = createTypeGuard('union');
export const isEnumItem = createTypeGuard('enum');
export const isTraitItem = createTypeGuard('trait');
export const isStaticItem = createTypeGuard('static');
export const isModuleItem = createTypeGuard('module');
export const isFunctionItem = createTypeGuard('function');
export const isConstantItem = createTypeGuard('constant');