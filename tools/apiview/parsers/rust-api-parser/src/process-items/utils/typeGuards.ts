import { Item, Impl, Struct, Union, Enum, Trait, Static, Module, Function, Constant, Type } from "../../../rustdoc-types/output/rustdoc-types";

export interface TypedItem<T> extends Omit<Item, 'inner'> {
  inner: { [K in keyof T]: T[K] } & Item['inner']
}

interface ItemInner<T> {
  [key: string]: T;
}

export interface UseInner extends ItemInner<{
  source?: string;
  is_glob: boolean;
}> { }

export interface ImplInner extends ItemInner<Impl> { }
export interface StructInner extends ItemInner<Struct> { }
export interface UnionInner extends ItemInner<Union> { }
export interface EnumInner extends ItemInner<Enum> { }
export interface TraitInner extends ItemInner<Trait> { }
export interface StaticInner extends ItemInner<Static> { }
export interface ModuleInner extends ItemInner<Module> { }
export interface FunctionInner extends ItemInner<Function> { }
export interface ConstantInner extends ItemInner<{ type: Type; const: Constant }> { }

export function isItemType<T>(key: keyof T) {
  return (item: Item): item is TypedItem<T> => {
    return typeof item.inner === "object" && item.inner !== null && key in item.inner;
  };
}

export const isUseItem = isItemType<UseInner>('use');
export const isImplItem = isItemType<ImplInner>('impl');
export const isStructItem = isItemType<StructInner>('struct');
export const isUnionItem = isItemType<UnionInner>('union');
export const isEnumItem = isItemType<EnumInner>('enum');
export const isTraitItem = isItemType<TraitInner>('trait');
export const isStaticItem = isItemType<StaticInner>('static');
export const isModuleItem = isItemType<ModuleInner>('module');
export const isFunctionItem = isItemType<FunctionInner>('function');
export const isConstantItem = isItemType<ConstantInner>('constant');
