import { Type } from "../../../rustdoc-types/output/rustdoc-types";
import { shouldElideLifetime } from "./shouldElideLifeTime";

export function typeToString(type: Type): string {
  if (!type) {
    return "unknown";
  }

  if (typeof type === "string") {
    return type;
  } else if ("resolved_path" in type) {
    let result = type.resolved_path.name;
    if (type.resolved_path.args && "angle_bracketed" in type.resolved_path.args) {
      const args = type.resolved_path.args.angle_bracketed.args
        .filter((arg) => typeof arg === "object" && "type" in arg)
        .map((arg) => typeToString(arg.type))
        .join(", ");
      if (args) {
        result += `<${args}>`;
      }
    }
    // TODO: angle_bracketed branch is covered, but parenthesized branch is not
    // TODO: use type.resolved_path.id for navigation if applicable
    return result;
  } else if ("dyn_trait" in type) {
    return `(dyn ${type.dyn_trait.traits.map((t) => t.trait.name).join(" + ")})`;
    // TODO: lifetime param is not being used
    // TODO: Can extend this to include navigation info; example: &(dyn MyTrait + Sync)
  } else if ("generic" in type) {
    return type.generic;
  } else if ("primitive" in type) {
    return type.primitive;
  } else if ("function_pointer" in type) {
    return `unknown`;
    // TODO: fix this later
  } else if ("tuple" in type) {
    return `(${type.tuple.map(typeToString).join(", ")})`;
  } else if ("slice" in type) {
    return `[${typeToString(type.slice)}]`;
  } else if ("array" in type) {
    return `[${typeToString(type.array.type)}; ${type.array.len}]`;
    // TODO: add example in the rust repo
  } else if ("pat" in type) {
    return `${typeToString(type.pat.type)} /* ${type.pat.__pat_unstable_do_not_use} */`;
    // TODO: add an example in the rust repo
  } else if ("impl_trait" in type) {
    return `impl ${type.impl_trait
      .map((b) => {
        if ("trait_bound" in b) {
          return b.trait_bound.trait.name;
        } else if ("outlives" in b) {
          return b.outlives;
        } else if ("use" in b) {
          return b.use.join(", ");
        }
      })
      .join(" + ")}`;
    // TODO: add an example in the rust repo
  } else if ("raw_pointer" in type) {
    return `*${type.raw_pointer.is_mutable ? "mut" : "const"} ${typeToString(type.raw_pointer.type)}`;
    // TODO: add an example in the rust repo
  } else if ("borrowed_ref" in type) {
    const lifetime = type.borrowed_ref.lifetime;
    const elidedLifetime = lifetime && !shouldElideLifetime(lifetime) ? `${lifetime} ` : "";
    return `&${type.borrowed_ref.is_mutable ? "mut " : ""}${elidedLifetime}${typeToString(type.borrowed_ref.type)}`;
    // TODO: add an example in the rust repo
  } else if ("qualified_path" in type) {
    return `${typeToString(type.qualified_path.self_type)} as ${type.qualified_path.trait ? type.qualified_path.trait.name + "::" : ""}${type.qualified_path.name}`;
    // TODO: args: GenericArgs is not being used
    // TODO: trait.args is not being used
    // TODO: add an example in the rust repo
  } else {
    return "unknown";
  }
}
