import { Item, Type } from "../utils/rustdoc-json-types/jsonTypes";
import { ReviewToken, TokenKind } from "../utils/apiview-models";

function typeToString(type: Type): string {
    if (!type) {
        return "unknown";
    }

    if (typeof type === "string") {
        return type;
    } else if ("resolved_path" in type) {
        return type.resolved_path.name;
    } else if ("dyn_trait" in type) {
        return `dyn ${type.dyn_trait.traits.map(t => t.trait.name).join(" + ")}`;
    } else if ("generic" in type) {
        return type.generic;
    } else if ("primitive" in type) {
        return type.primitive;
    } else if ("function_pointer" in type) {
        return `unknown`; // TODO: fix this later
    } else if ("tuple" in type) {
        return `(${type.tuple.types.map(typeToString).join(", ")})`;
    } else if ("slice" in type) {
        return `[${typeToString(type.slice.type)}]`;
    } else if ("array" in type) {
        return `[${typeToString(type.array.type)}; ${type.array.len}]`;
    } else if ("pat" in type) {
        return `${typeToString(type.pat.type)} /* ${type.pat.__pat_unstable_do_not_use} */`;
    } else if ("impl_trait" in type) {
        return `impl ${type.impl_trait.bounds.map(b => {
            if ('trait_bound' in b) {
                return b.trait_bound.trait.name;
            } else if ('outlives' in b) {
                return b.outlives.lifetime;
            } else if ('use' in b) {
                return b.use.lifetimes.join(", ");
            } else {
                return "unknown";
            }
        }).join(" + ")}`;
    } else if ("raw_pointer" in type) {
        return `*${type.raw_pointer.is_mutable ? "mut" : "const"} ${typeToString(type.raw_pointer.type)}`;
    } else if ("borrowed_ref" in type) {
        return `&${type.borrowed_ref.is_mutable ? "mut " : ""}${type.borrowed_ref.lifetime ? `'${type.borrowed_ref.lifetime} ` : ""}${typeToString(type.borrowed_ref.type)}`;
    } else if ("qualified_path" in type) {
        return `${typeToString(type.qualified_path.self_type)} as ${type.qualified_path.trait ? type.qualified_path.trait.name + "::" : ""}${type.qualified_path.name}`;
    } else {
        return "unknown";
    }
}

export function processStructField(struct_field: Type): ReviewToken {
    // Add the struct field type
    const reviewToken: ReviewToken = {
        Kind: TokenKind.TypeName,
        Value: `${typeToString(struct_field)}`,
        HasSuffixSpace: false
    };
    return reviewToken;
}