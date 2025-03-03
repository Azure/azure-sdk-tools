import { Type } from "../../../rustdoc-types/output/rustdoc-types";
import { ReviewToken, TokenKind } from "../../models/apiview-models";
import { processGenericArgs } from "./processGenerics";
import { shouldElideLifetime } from "./shouldElideLifeTime";

export function typeToReviewTokens(type: Type): ReviewToken[] {
  if (!type) {
    return [{ Kind: TokenKind.Text, Value: "unknown", HasSuffixSpace: false }];
  }

  if (typeof type === "string") {
    return [{ Kind: TokenKind.TypeName, Value: type, HasSuffixSpace: false }];
  } else if ("resolved_path" in type) {
    if (!type.resolved_path.args) {
      return [
        {
          Kind: TokenKind.TypeName,
          Value: type.resolved_path.name,
          HasSuffixSpace: false,
          NavigateToId: type.resolved_path.id.toString(),
        },
      ];
    }

    // Create base token for the path name
    let result: ReviewToken[] = [
      {
        Kind: TokenKind.TypeName,
        Value: type.resolved_path.name,
        HasSuffixSpace: false,
        NavigateToId: type.resolved_path.id.toString(),
      },
    ];

    result.push(...processGenericArgs(type.resolved_path.args));
    return result;
  } else if ("dyn_trait" in type) {
    const tokens = [
      { Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false },
      { Kind: TokenKind.Keyword, Value: "dyn" },
      ...type.dyn_trait.traits.flatMap((t, i) => [
        {
          Kind: TokenKind.TypeName,
          Value: t.trait.name,
          HasSuffixSpace: false,
          NavigateToId: t.trait.id.toString(),
        },
        i < type.dyn_trait.traits.length - 1
          ? { Kind: TokenKind.Punctuation, Value: "+ ", HasSuffixSpace: false }
          : { Kind: TokenKind.Text, Value: "", HasSuffixSpace: false },
      ]),
    ];

    // Add lifetime if present
    if (type.dyn_trait.lifetime && !shouldElideLifetime(type.dyn_trait.lifetime)) {
      tokens.push({ Kind: TokenKind.Punctuation, Value: "+ ", HasSuffixSpace: false });
      tokens.push({ Kind: TokenKind.Text, Value: type.dyn_trait.lifetime, HasSuffixSpace: false });
    }

    tokens.push({ Kind: TokenKind.Punctuation, Value: ")", HasSuffixSpace: false });

    return tokens;
  } else if ("generic" in type) {
    return [{ Kind: TokenKind.TypeName, Value: type.generic, HasSuffixSpace: false }];
  } else if ("primitive" in type) {
    return [{ Kind: TokenKind.TypeName, Value: type.primitive, HasSuffixSpace: false }];
  } else if ("function_pointer" in type) {
    const tokens: ReviewToken[] = [];

    // Add generic parameters (for<'a> etc)
    if (type.function_pointer.generic_params.length > 0) {
      tokens.push({ Kind: TokenKind.Keyword, Value: "for", HasSuffixSpace: false });
      tokens.push({ Kind: TokenKind.Punctuation, Value: "<", HasSuffixSpace: false });

      type.function_pointer.generic_params.forEach((param, index) => {
        tokens.push({ Kind: TokenKind.Text, Value: param.name, HasSuffixSpace: false });

        if (index < type.function_pointer.generic_params.length - 1) {
          tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
        }
      });

      tokens.push({ Kind: TokenKind.Punctuation, Value: "> ", HasSuffixSpace: false });
    }

    // Add function modifiers
    const header = type.function_pointer.header;

    // Handle unsafe
    if (header.is_unsafe) {
      tokens.push({ Kind: TokenKind.Keyword, Value: "unsafe", HasSuffixSpace: true });
    }

    // Handle extern ABI
    if (header.abi !== "Rust") {
      tokens.push({ Kind: TokenKind.Keyword, Value: "extern", HasSuffixSpace: true });

      let abiString = "";
      if (typeof header.abi === "string") {
        // For simple ABI strings
        abiString = header.abi;
      } else {
        // For complex ABI objects
        const abiKey = Object.keys(header.abi)[0];
        if (abiKey) {
          abiString = abiKey;
        }
      }

      if (abiString && abiString !== "Rust") {
        tokens.push({ Kind: TokenKind.Text, Value: `"${abiString}"`, HasSuffixSpace: true });
      }
    }

    // Handle const
    if (header.is_const) {
      tokens.push({ Kind: TokenKind.Keyword, Value: "const", HasSuffixSpace: true });
    }

    // Handle async
    if (header.is_async) {
      tokens.push({ Kind: TokenKind.Keyword, Value: "async", HasSuffixSpace: true });
    }

    // Add fn keyword
    tokens.push({ Kind: TokenKind.Keyword, Value: "fn", HasSuffixSpace: false });

    // Add parameters
    tokens.push({ Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false });

    const signature = type.function_pointer.sig;
    signature.inputs.forEach(([paramName, paramType], index) => {
      // Add parameter name if present
      if (paramName) {
        tokens.push({ Kind: TokenKind.Text, Value: paramName, HasSuffixSpace: false });
        tokens.push({ Kind: TokenKind.Punctuation, Value: ": ", HasSuffixSpace: false });
      }

      // Add parameter type
      tokens.push(...typeToReviewTokens(paramType));

      // Add comma if not the last parameter
      if (index < signature.inputs.length - 1) {
        tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
      }
    });

    // Add C-variadic "..." if needed
    if (signature.is_c_variadic) {
      if (signature.inputs.length > 0) {
        tokens.push({ Kind: TokenKind.Punctuation, Value: ", ", HasSuffixSpace: false });
      }
      tokens.push({ Kind: TokenKind.Punctuation, Value: "...", HasSuffixSpace: false });
    }

    tokens.push({ Kind: TokenKind.Punctuation, Value: ")", HasSuffixSpace: false });

    // Add return type if present
    if (signature.output) {
      tokens.push({ Kind: TokenKind.Punctuation, Value: " -> ", HasSuffixSpace: false });
      tokens.push(...typeToReviewTokens(signature.output));
    }

    return tokens;
  } else if ("tuple" in type) {
    return [
      { Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false },
      ...type.tuple.flatMap((t, i) => [
        ...typeToReviewTokens(t),
        i < type.tuple.length - 1
          ? { Kind: TokenKind.Punctuation, Value: "," }
          : { Kind: TokenKind.Text, Value: "", HasSuffixSpace: false },
      ]),
      { Kind: TokenKind.Punctuation, Value: ")", HasSuffixSpace: false },
    ];
  } else if ("slice" in type) {
    return [
      { Kind: TokenKind.Punctuation, Value: "[", HasSuffixSpace: false },
      ...typeToReviewTokens(type.slice),
      { Kind: TokenKind.Punctuation, Value: "]", HasSuffixSpace: false },
    ];
  } else if ("array" in type) {
    return [
      { Kind: TokenKind.Punctuation, Value: "[", HasSuffixSpace: false },
      ...typeToReviewTokens(type.array.type),
      { Kind: TokenKind.Punctuation, Value: ";" },
      { Kind: TokenKind.Text, Value: type.array.len.toString(), HasSuffixSpace: false },
      { Kind: TokenKind.Punctuation, Value: "]", HasSuffixSpace: false },
    ];
    // TODO: add example in the rust repo
  } else if ("pat" in type) {
    return [
      ...typeToReviewTokens(type.pat.type),
      { Kind: TokenKind.Text, Value: " /*" },
      { Kind: TokenKind.Text, Value: type.pat.__pat_unstable_do_not_use, HasSuffixSpace: false },
      { Kind: TokenKind.Text, Value: " */", HasSuffixSpace: false },
    ];
    // TODO: add an example in the rust repo
  } else if ("impl_trait" in type) {
    return [
      { Kind: TokenKind.Keyword, Value: "impl" },
      ...type.impl_trait.flatMap((b, i) => {
        if ("trait_bound" in b) {
          return [
            { Kind: TokenKind.TypeName, Value: b.trait_bound.trait.name },
            i < type.impl_trait.length - 1
              ? { Kind: TokenKind.Punctuation, Value: "+", HasSuffixSpace: false }
              : { Kind: TokenKind.Text, Value: "", HasSuffixSpace: false },
          ];
        } else if ("outlives" in b) {
          return [{ Kind: TokenKind.Text, Value: b.outlives, HasSuffixSpace: false }];
        } else if ("use" in b) {
          return [
            ...b.use.flatMap((u, j) => [
              { Kind: TokenKind.Text, Value: u, HasSuffixSpace: false },
              j < b.use.length - 1
                ? { Kind: TokenKind.Punctuation, Value: "," }
                : { Kind: TokenKind.Text, Value: "", HasSuffixSpace: false },
            ]),
          ];
        }
      }),
    ];

    // TODO: add an example in the rust repo
  } else if ("raw_pointer" in type) {
    return [
      { Kind: TokenKind.Punctuation, Value: "*", HasSuffixSpace: false },
      {
        Kind: TokenKind.Keyword,
        Value: type.raw_pointer.is_mutable ? "mut" : "const",
        HasSuffixSpace: false,
      },
      ...typeToReviewTokens(type.raw_pointer.type),
    ];
    // TODO: add an example in the rust repo
  } else if ("borrowed_ref" in type) {
    const lifetime = type.borrowed_ref.lifetime;
    const elidedLifetime = lifetime && !shouldElideLifetime(lifetime) ? `${lifetime} ` : "";
    return [
      { Kind: TokenKind.Punctuation, Value: "&", HasSuffixSpace: false },
      {
        Kind: TokenKind.Keyword,
        Value: type.borrowed_ref.is_mutable ? "mut" : "",
        HasSuffixSpace: false,
      },
      { Kind: TokenKind.Text, Value: elidedLifetime, HasSuffixSpace: false },
      ...typeToReviewTokens(type.borrowed_ref.type),
    ];
    // TODO: add an example in the rust repo
  } else if ("qualified_path" in type) {
    return [
      ...typeToReviewTokens(type.qualified_path.self_type),
      { Kind: TokenKind.Text, Value: " as ", HasSuffixSpace: false },
      {
        Kind: TokenKind.TypeName,
        Value: type.qualified_path.trait ? type.qualified_path.trait.name + "::" : "",
        HasSuffixSpace: false,
      },
      { Kind: TokenKind.TypeName, Value: type.qualified_path.name, HasSuffixSpace: false },
    ];
    // TODO: args: GenericArgs is not being used
    // TODO: trait.args is not being used
    // TODO: add an example in the rust repo
  } else {
    // return "unknown";
    return [{ Kind: TokenKind.Text, Value: "unknown" }];
  }
}
