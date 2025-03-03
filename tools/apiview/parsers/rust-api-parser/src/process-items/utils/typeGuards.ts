import {
  Item,
  Impl,
  Struct,
  Union,
  Enum,
  Trait,
  Static,
  Module,
  Function,
  Constant,
  Type,
  Use,
  TraitAlias,
  Generics,
  GenericBound,
  Variant,
  TypeAlias,
  ProcMacro,
  Primitive,
} from "../../../rustdoc-types/output/rustdoc-types";

type ItemTypes = {
  module: Module;
  extern_crate: { name: string; rename?: string };
  use: Use;
  union: Union;
  struct: Struct;
  struct_field: Type;
  enum: Enum;
  variant: Variant;
  function: Function;
  trait: Trait;
  trait_alias: TraitAlias;
  impl: Impl;
  type_alias: TypeAlias;
  constant: { type: Type; const: Constant };
  static: Static;
  extern_type: "extern_type";
  macro: string;
  proc_macro: ProcMacro;
  primitive: Primitive;
  assoc_const: { type: Type; value?: string };
  assoc_type: { generics: Generics; bounds: GenericBound[]; type?: Type };
};

type ItemTypeGuard<K extends keyof ItemTypes> = (
  item: Item,
) => item is Item & { inner: { [P in K]: ItemTypes[K] } };

const createTypeGuard =
  <K extends keyof ItemTypes>(type: K): ItemTypeGuard<K> =>
  (item: Item): item is Item & { inner: { [P in K]: ItemTypes[K] } } =>
    item &&
    typeof item === "object" &&
    item.inner &&
    typeof item.inner === "object" &&
    type in item.inner;

export const isUseItem = createTypeGuard("use");
export const isImplItem = createTypeGuard("impl");
export const isStructItem = createTypeGuard("struct");
export const isUnionItem = createTypeGuard("union");
export const isEnumItem = createTypeGuard("enum");
export const isTraitItem = createTypeGuard("trait");
export const isTraitAliasItem = createTypeGuard("trait_alias");
export const isStaticItem = createTypeGuard("static");
export const isModuleItem = createTypeGuard("module");
export const isFunctionItem = createTypeGuard("function");
export const isConstantItem = createTypeGuard("constant");
export const isAssocTypeItem = createTypeGuard("assoc_type");
export const isTypeAliasItem = createTypeGuard("type_alias");
export const isVariantItem = createTypeGuard("variant");
export const isStructFieldItem = createTypeGuard("struct_field");
export const isPrimitiveItem = createTypeGuard("primitive");
export const isExternCrateItem = createTypeGuard("extern_crate");
export const isAssocConstItem = createTypeGuard("assoc_const");
export const isProcMacroItem = createTypeGuard("proc_macro");
export const isMacroItem = createTypeGuard("macro");
export const isExternTypeItem = createTypeGuard("extern_type");
