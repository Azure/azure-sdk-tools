// This file is adapted from the Rust repository (link below).
// https://github.com/rust-lang/rust/blob/fb65a3ee576feab95a632eb062f466d7a0342310/src/rustdoc-json-types/lib.rs
// Future changes to rustdoc may require updates to remain compatible.
// FORMAT_VERSION on line 14 indicates the JSON output version (search for "format_version" in rustdoc’s JSON output).


/**
 * The version of JSON output that this crate represents.
 *
 * This integer is incremented with every breaking change to the API,
 * and is returned along with the JSON blob as Crate.format_version.
 * Consuming code should assert that this value matches the format version(s) that it supports.
 */
export const FORMAT_VERSION: number = 37;

/**
 * A re-export for use in src/librustdoc
 * (In Rust, this is a type alias for a hashmap. Here, just use a Record.)
 */
export type FxHashMap<K extends string | number | symbol, V> = Record<K, V>;

/**
 * The root of the emitted JSON blob.
 *
 * It contains all type/documentation information about the language items in the local crate,
 * as well as info about external items to allow tools to find or link to them.
 */
export interface Crate {
    /**
     * The id of the root Module item of the local crate.
     */
    root: number;
    /**
     * The version string given to --crate-version, if any.
     */
    crate_version: string | null;
    /**
     * Whether or not the output includes private items.
     */
    includes_private: boolean;
    /**
     * A collection of all items in the local crate as well as some external traits
     * and their items that are referenced locally.
     */
    index: Record<string, Item>;
    /**
     * Maps IDs to fully qualified paths and other info helpful for generating links.
     */
    paths: Record<string, ItemSummary>;
    /**
     * Maps crate_id of items to a crate name and html_root_url if it exists.
     */
    external_crates: Record<number, ExternalCrate>;
    /**
     * A single version number to be used in the future when making backwards incompatible changes
     * to the JSON output.
     */
    format_version: number;
}

/**
 * Metadata of a crate, either the same crate on which rustdoc was invoked, or its dependency.
 */
export interface ExternalCrate {
    /**
     * The name of the crate.
     */
    name: string;
    /**
     * The root URL at which the crate's documentation lives.
     */
    html_root_url: string | null;
}

/**
 * Information about an external (not defined in the local crate) Item.
 *
 * For external items, you don't get the same level of information. This struct should contain
 * enough to generate a link/reference to the item in question, or can be used by a tool that
 * takes the json output of multiple crates to find the actual item definition with all the
 * relevant info.
 */
export interface ItemSummary {
    /**
     * Can be used to look up the name and html_root_url of the crate this item came from
     * in the external_crates map.
     */
    crate_id: number;
    /**
     * The list of path components for the fully qualified path of this item.
     */
    path: string[];
    /**
     * Whether this item is a struct, trait, macro, etc.
     */
    kind: ItemKind;
}

/**
 * Anything that can hold documentation - modules, structs, enums, functions, traits, etc.
 *
 * The Item data type holds fields that can apply to any of these,
 * and leaves kind-specific details (like function args or enum variants) to the inner field.
 */
export interface Item {
    /**
     * The unique identifier of this item. Can be used to find this item in various mappings.
     */
    id: number;
    /**
     * This can be used as a key to the external_crates map of Crate to see which crate this item
     * came from.
     */
    crate_id: number;
    /**
     * Some items such as impls don't have names.
     */
    name: string | null;
    /**
     * The source location of this item (absent if it came from a macro expansion or inline assembly).
     */
    span: Span | null;
    /**
     * By default all documented items are public, but you can tell rustdoc to output private items
     * so this field is needed to differentiate.
     */
    visibility: Visibility;
    /**
     * The full markdown docstring of this item. Absent if there is no documentation at all,
     * Some("") if there is some documentation but it is empty (EG #[doc = ""]).
     */
    docs: string | null;
    /**
     * This mapping resolves intra-doc links from the docstring to their IDs
     */
    links: Record<string, number>;
    /**
     * Stringified versions of the attributes on this item (e.g. "#[inline]")
     */
    attrs: string[];
    /**
     * Information about the item's deprecation, if present.
     */
    deprecation: Deprecation | null;
    /**
     * The type-specific fields describing this item.
     */
    inner: ItemEnum;
}

/**
 * A range of source code.
 */
export interface Span {
    /**
     * The path to the source file for this span relative to the path rustdoc was invoked with.
     */
    filename: string;
    /**
     * Zero indexed Line and Column of the first character of the Span
     */
    begin: [number, number];
    /**
     * Zero indexed Line and Column of the last character of the Span
     */
    end: [number, number];
}

/**
 * Information about the deprecation of an Item.
 */
export interface Deprecation {
    /**
     * Usually a version number when this Item first became deprecated.
     */
    since: string | null;
    /**
     * The reason for deprecation and/or what alternatives to use.
     */
    note: string | null;
}

/**
 * Visibility of an Item.
 */
export type Visibility =
    | "public"
    | "default"
    | "crate"
    | {
        restricted: {
            parent: number;
            path: string;
        };
    };

/**
 * Dynamic trait object type (dyn Trait).
 */
export interface DynTrait {
    /**
     * All the traits implemented. One of them is the vtable, and the rest must be auto traits.
     */
    traits: PolyTrait[];
    /**
     * The lifetime of the whole dyn object
     */
    lifetime: string | null;
}

/**
 * A trait and potential HRTBs
 */
export interface PolyTrait {
    /**
     * The path to the trait.
     */
    trait_: Path;
    /**
     * Used for Higher-Rank Trait Bounds (HRTBs)
     */
    generic_params: GenericParamDef[];
}

/**
 * A set of generic arguments provided to a path segment.
 */
export type GenericArgs =
    | {
        'angle_bracketed': {
            args: GenericArg[];
            bindings: AssocItemConstraint[];
        }
    }
    | {
        'parenthesized': {
            inputs: Type[];
            output: Type | null;
        }
    };

/**
 * One argument in a list of generic arguments to a path segment.
 */
export type GenericArg =
    | {
        type: Type;
    }
    | {
        lifetime: string;
    }
    | {
        const: Constant;
    }
    | "infer"

/**
 * A constant.
 */
export interface Constant {
    /**
     * The stringified expression of this constant. Note that its mapping to the original
     * source code is unstable and it's not guaranteed to match the source code.
     */
    expr: string;
    /**
     * The value of the evaluated expression for this constant, which is only computed for numeric types.
     */
    value: string | null;
    /**
     * Whether this constant is a bool, numeric, string, or char literal.
     */
    is_literal: boolean;
}

/**
 * Describes a bound applied to an associated type/constant.
 */
export interface AssocItemConstraint {
    /**
     * The name of the associated type/constant.
     */
    name: string;
    /**
     * Arguments provided to the associated type/constant.
     */
    args: GenericArgs;
    /**
     * The kind of bound applied to the associated type/constant.
     */
    binding: AssocItemConstraintKind;
}

/**
 * The way in which an associate type/constant is bound.
 */
export type AssocItemConstraintKind =
    | {
        'equality': {
            term: Term;
        }
    }
    | {
        'constraint': {
            bounds: GenericBound[];
        }
    };

/**
 * The fundamental kind of an item. Unlike ItemEnum, this does not carry any additional info.
 *
 * Part of ItemSummary.
 */
export type ItemKind =
    | 'module'
    | 'extern_crate'
    | 'use'
    | 'struct'
    | 'struct_field'
    | 'union'
    | 'enum'
    | 'variant'
    | 'function'
    | 'type_alias'
    | 'constant'
    | 'trait'
    | 'trait_alias'
    | 'impl'
    | 'static'
    | 'extern_type'
    | 'macro'
    | 'proc_attribute'
    | 'proc_derive'
    | 'assoc_const'
    | 'assoc_type'
    | 'primitive'
    | 'keyword';

/**
 * Specific fields of an item.
 *
 * Part of Item.
 */
export type ItemEnum =
    | {
        module: Module;
    }
    | {
        extern_crate: {
            name: string;
            rename: string | null;
        }
    }
    | {
        use: Use;
    }
    | {
        union: Union;
    }
    | {
        struct: Struct;
    }
    | {
        struct_field: Type;
    }
    | {
        enum: EnumType;
    }
    | {
        variant: Variant;
    }
    | {
        function: FunctionItem;
    }
    | {
        trait: TraitItem;
    }
    | {
        trait_alias: TraitAlias;
    }
    | {
        impl: Impl;
    }
    | {
        type_alias: TypeAlias;
    }
    | {
        constant: {
            type_: Type;
            const_: Constant;
        }
    }
    | {
        static: StaticItem;
    }
    | "extern_type"
    | {
        macro: string;
    }
    | {
        proc_macro: ProcMacro;
    }
    | {
        primitive: Primitive;
    }
    | {
        assoc_const: {
            type_: Type;
            value: string | null;
        }
    }
    | {
        assoc_type: {
            generics: Generics;
            bounds: GenericBound[];
            type_: Type | null;
        }
    };

/**
 * A module declaration, e.g. `mod foo;` or `mod foo {}`.
 */
export interface Module {
    /**
     * Whether this is the root item of a crate.
     */
    is_crate: boolean;
    /**
     * Items declared inside this module.
     */
    items: number[];
    /**
     * If true, this module is not part of the public API, but it contains
     * items that are re-exported as public API.
     */
    is_stripped: boolean;
}

/**
 * A union.
 */
export interface Union {
    /**
     * The generic parameters and where clauses on this union.
     */
    generics: Generics;
    /**
     * Whether any fields have been removed from the result, due to being private or hidden.
     */
    has_stripped_fields: boolean;
    /**
     * The list of fields in the union.
     * All of the corresponding Item values are of kind struct_field.
     */
    fields: number[];
    /**
     * All impls (both of traits and inherent) for this union.
     */
    impls: number[];
}

/**
 * A struct.
 */
export interface Struct {
    /**
     * The kind of the struct (e.g. unit, tuple-like or struct-like) and the data specific to it.
     */
    kind: StructKind;
    /**
     * The generic parameters and where clauses on this struct.
     */
    generics: Generics;
    /**
     * All impls (both of traits and inherent) for this struct.
     */
    impls: number[];
}

/**
 * The kind of a Struct and the data specific to it, i.e. fields.
 */
export type StructKind =
    | "unit"
    | {
        tuple: {
            fields: Array<number | null>;
        };
    }
    | {
        plain: {
            fields: number[];
            has_stripped_fields: boolean;
        };
    };

/**
 * An enum.
 */
export interface EnumType {
    /**
     * Information about the type parameters and `where` clauses of the enum.
     */
    generics: Generics;
    /**
     * Whether any variants have been removed from the result.
     */
    has_stripped_variants: boolean;
    /**
     * The list of variants in the enum.
     */
    variants: number[];
    /**
     * impls for the enum.
     */
    impls: number[];
}

/**
 * A variant of an enum.
 */
export interface Variant {
    /**
     * Whether the variant is plain, a tuple-like, or struct-like. Contains the fields.
     */
    kind: VariantKind;
    /**
     * The discriminant, if explicitly specified.
     */
    discriminant: Discriminant | null;
}

/**
 * The kind of an Enum Variant and the data specific to it, i.e. fields.
 */
export type VariantKind =
    | "plain"
    | {
        tuple: {
            fields: Array<number | null>;
        };
    }
    | {
        struct: {
            fields: number[];
            has_stripped_fields: boolean;
        };
    };
/**
 * The value that distinguishes a variant in an Enum from other variants.
 */
export interface Discriminant {
    /**
     * The expression that produced the discriminant. This preserves the original formatting.
     */
    expr: string;
    /**
     * The numerical value of the discriminant. Stored as a string due to JSON's poor support
     * for large integers.
     */
    value: string;
}

/**
 * A set of fundamental properties of a function.
 */
export interface FunctionHeader {
    /**
     * Is this function marked as const?
     */
    is_const: boolean;
    /**
     * Is this function unsafe?
     */
    is_unsafe: boolean;
    /**
     * Is this function async?
     */
    is_async: boolean;
    /**
     * The ABI used by the function.
     */
    abi: Abi;
}

/**
 * The ABI (Application Binary Interface) used by a function.
 */
export type Abi =
    | "Rust"
    | {
        c: {
            unwind: boolean;
        };
    }
    | {
        cdecl: {
            unwind: boolean;
        };
    }
    | {
        stdcall: {
            unwind: boolean;
        };
    }
    | {
        fastcall: {
            unwind: boolean;
        };
    }
    | {
        aapcs: {
            unwind: boolean;
        };
    }
    | {
        win64: {
            unwind: boolean;
        };
    }
    | {
        sysv64: {
            unwind: boolean;
        };
    }
    | {
        system: {
            unwind: boolean;
        };
    }
    | {
        other: {
            name: string;
        };
    };

/**
 * A function declaration (including methods and other associated functions).
 */
export interface FunctionItem {
    /**
     * Information about the function signature, or declaration.
     */
    sig: FunctionSignature;
    /**
     * Information about the function’s type parameters and `where` clauses.
     */
    generics: Generics;
    /**
     * Information about core properties of the function, e.g. whether it's const, its ABI, etc.
     */
    header: FunctionHeader;
    /**
     * Whether the function has a body.
     */
    has_body: boolean;
}

/**
 * Generic parameters accepted by an item and where clauses imposed on it and the parameters.
 */
export interface Generics {
    /**
     * A list of generic parameter definitions.
     */
    params: GenericParamDef[];
    /**
     * A list of where predicates.
     */
    where_predicates: WherePredicate[];
}

/**
 * One generic parameter accepted by an item.
 */
export interface GenericParamDef {
    /**
     * Name of the parameter.
     */
    name: string;
    /**
     * The kind of the parameter and data specific to a particular parameter kind.
     */
    kind: GenericParamDefKind;
}

/**
 * The kind of a GenericParamDef.
 */
export type GenericParamDefKind =
    | {
        lifetime: {
            outlives: string[];
        };
    }
    | {
        type: {
            bounds: GenericBound[];
            default: Type | null;
            is_synthetic: boolean;
        };
    }
    | {
        const: {
            type_: Type;
            default: string | null;
        };
    };
/**
 * One where clause.
 */
export type WherePredicate =
    | {
        bound_predicate: {
            type: Type;
            bounds: GenericBound[];
            generic_params: GenericParamDef[];
        };
    }
    | {
        lifetime_predicate: {
            lifetime: string;
            outlives: string[];
        };
    }
    | {
        eq_predicate: {
            lhs: Type;
            rhs: Term;
        };
    };

/**
 * Either a trait bound or a lifetime bound.
 */
export type GenericBound =
    | {
        trait_bound: {
            trait: Path;
            generic_params: GenericParamDef[];
            modifier: TraitBoundModifier;
        };
    }
    | {
        outlives: {
            lifetime: string;
        };
    }
    | {
        use: {
            lifetimes: string[];
        };
    };

/**
 * A set of modifiers applied to a trait.
 */
export type TraitBoundModifier = 'none' | 'maybe' | 'maybe_const';

/**
 * Either a type or a constant, usually stored as the right-hand side of an equation.
 */
export type Term =
    | {
        type: Type;
    }
    | {
        constant: Constant;
    };

/**
 * A type.
 */
export type Type =
    | {
        resolved_path: Path;
    }
    | {
        dyn_trait: DynTrait;
    }
    | {
        generic: string
    }
    | {
        primitive: string
    }
    | {
        function_pointer: FunctionPointer;
    }
    | {
        tuple: {
            types: Type[];
        };
    }
    | {
        slice: {
            type_: Type;
        };
    }
    | {
        array: {
            type_: Type;
            len: string;
        };
    }
    | {
        pat: {
            type_: Type;
            __pat_unstable_do_not_use: string;
        };
    }
    | {
        impl_trait: {
            bounds: GenericBound[];
        };
    }
    | "infer"
    | {
        raw_pointer: {
            is_mutable: boolean;
            type_: Type;
        };
    }
    | {
        borrowed_ref: {
            lifetime: string | null;
            is_mutable: boolean;
            type_: Type;
        };
    }
    | {
        qualified_path: {
            name: string;
            args: GenericArgs;
            self_type: Type;
            trait_: Path | null;
        };
    };

/**
 * A type that has a simple path to it. This is the kind of type of structs, unions, enums, etc.
 */
export interface Path {
    /**
     * The name of the type as declared.
     */
    name: string;
    /**
     * The ID of the type.
     */
    id: number;
    /**
     * Generic arguments to the type.
     */
    args: GenericArgs | null;
}

/**
 * A type that is a function pointer.
 */
export interface FunctionPointer {
    /**
     * The signature of the function.
     */
    sig: FunctionSignature;
    /**
     * Used for Higher-Rank Trait Bounds (HRTBs)
     */
    generic_params: GenericParamDef[];
    /**
     * The core properties of the function, such as the ABI, whether it's unsafe, etc.
     */
    header: FunctionHeader;
}

/**
 * The signature of a function.
 */
export interface FunctionSignature {
    /**
     * List of argument names and their type.
     */
    inputs: Array<[string, Type]>;
    /**
     * The output type, if specified.
     */
    output: Type | null;
    /**
     * Whether the function accepts an arbitrary amount of trailing arguments the C way.
     */
    is_c_variadic: boolean;
}

/**
 * A trait declaration.
 */
export interface TraitItem {
    /**
     * Whether the trait is marked auto.
     */
    is_auto: boolean;
    /**
     * Whether the trait is marked as unsafe.
     */
    is_unsafe: boolean;
    /**
     * Whether the trait is dyn compatible.
     */
    is_dyn_compatible: boolean;
    /**
     * Associated Items that can/must be implemented by the impl blocks.
     */
    items: number[];
    /**
     * Information about the type parameters and where clauses of the trait.
     */
    generics: Generics;
    /**
     * Constraints that must be met by the implementor of the trait.
     */
    bounds: GenericBound[];
    /**
     * The implementations of the trait.
     */
    implementations: number[];
}

/**
 * A trait alias declaration, e.g. `trait Int = Add + Sub + Mul + Div;`
 */
export interface TraitAlias {
    /**
     * Information about the type parameters and where clauses of the alias.
     */
    generics: Generics;
    /**
     * The bounds that are associated with the alias.
     */
    params: GenericBound[];
}

/**
 * An impl block.
 */
export interface Impl {
    /**
     * Whether this impl is for an unsafe trait.
     */
    is_unsafe: boolean;
    /**
     * Information about the impl’s type parameters and where clauses.
     */
    generics: Generics;
    /**
     * The list of the names of all the trait methods that weren't mentioned in this impl
     * but were provided by the trait itself.
     */
    provided_trait_methods: string[];
    /**
     * The trait being implemented or None if the impl is inherent.
     */
    trait_: Path | null;
    /**
     * The type that the impl block is for.
     */
    for_: Type;
    /**
     * The list of associated items contained in this impl block.
     */
    items: number[];
    /**
     * Whether this is a negative impl.
     */
    is_negative: boolean;
    /**
     * Whether this is an impl that’s implied by the compiler (for autotraits).
     */
    is_synthetic: boolean;
    /**
     * Blanket impl field (undocumented in detail).
     */
    blanket_impl: Type | null;
}

/**
 * A `use` statement.
 */
export interface Use {
    /**
     * The full path being imported.
     */
    source: string;
    /**
     * May be different from the last segment of source when renaming imports: use source as name;
     */
    name: string;
    /**
     * The ID of the item being imported. Will be None in case of re-exports of primitives.
     */
    id: number | null;
    /**
     * Whether this statement is a wildcard use, e.g. use source::*;
     */
    is_glob: boolean;
}

/**
 * A procedural macro.
 */
export interface ProcMacro {
    /**
     * How this macro is supposed to be called: foo!(), #[foo] or #[derive(foo)]
     */
    kind: MacroKind;
    /**
     * Helper attributes defined by a macro to be used inside it.
     */
    helpers: string[];
}

/**
 * The way a ProcMacro is declared to be used.
 */
export type MacroKind = 'bang' | 'attr' | 'derive';

/**
 * A type alias declaration.
 */
export interface TypeAlias {
    /**
     * The type referred to by this alias.
     */
    type_: Type;
    /**
     * Information about the type parameters and where clauses of the alias.
     */
    generics: Generics;
}

/**
 * A static declaration.
 */
export interface StaticItem {
    /**
     * The type of the static.
     */
    type_: Type;
    /**
     * This is true for mutable statics, declared as static mut X: T = f();
     */
    is_mutable: boolean;
    /**
     * The stringified expression for the initial value.
     */
    expr: string;
    /**
     * Is the static `unsafe`?
     */
    is_unsafe: boolean;
}

/**
 * A primitive type declaration. Declarations of this kind can only come from the core library.
 */
export interface Primitive {
    /**
     * The name of the type.
     */
    name: string;
    /**
     * The implementations, inherent and of traits, on the primitive type.
     */
    impls: number[];
}