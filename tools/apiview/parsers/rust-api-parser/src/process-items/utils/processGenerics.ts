import { ReviewToken, TokenKind } from "../../models/apiview-models";
import {
  Generics,
  GenericParamDef,
  GenericBound,
  WherePredicate,
  GenericArgs,
} from "../../../rustdoc-types/output/rustdoc-types";
import { shouldElideLifetime } from "./shouldElideLifeTime";
import { typeToReviewTokens } from "./typeToReviewTokens";
import { getPath } from "./pathUtils";

export function processGenerics(generics: Generics): {
  params: ReviewToken[];
  wherePredicates: ReviewToken[];
} {
  // Process where predicates if present
  const wherePredicates = generics.where_predicates;
  const whereTokens: ReviewToken[] = [];
  if (wherePredicates.length > 0) {
    whereTokens.push(
      { Kind: TokenKind.Keyword, Value: "where", HasSuffixSpace: true, HasPrefixSpace: true },
      ...createWherePredicatesTokens(wherePredicates),
    );
  }

  return { params: createGenericsParamsTokens(generics.params), wherePredicates: whereTokens };
}

function createGenericsParamsTokens(params: GenericParamDef[]): ReviewToken[] {
  return params.flatMap((param: GenericParamDef, index: number) => {
    const tokens: ReviewToken[] = [];
    if (index === 0) {
      tokens.push({ Kind: TokenKind.Text, Value: "<", HasSuffixSpace: false });
    }
    if (!("lifetime" in param.kind && shouldElideLifetime(param.name))) {
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: param.name,
        HasSuffixSpace: false,
      });
    }

    // Param bounds
    if ("type" in param.kind && param.kind.type.bounds && param.kind.type.bounds.length > 0) {
      tokens.push({ Kind: TokenKind.Text, Value: ":" });
      tokens.push(...createGenericBoundTokens(param.kind.type.bounds));
    }

    // Close generics if it's the last parameter
    if (index === params.length - 1) {
      tokens.push({ Kind: TokenKind.Text, Value: ">", HasSuffixSpace: false });
    } else if (tokens.length > 1) {
      // To account for "<"
      tokens.push({ Kind: TokenKind.Text, Value: "," });
    }

    return tokens;
  });
}

function createWherePredicatesTokens(wherePredicates: WherePredicate[]): ReviewToken[] {
  const result = wherePredicates.flatMap((predicate: WherePredicate, index: number) => {
    const tokens: ReviewToken[] = [];

    if ("bound_predicate" in predicate) {
      tokens.push(...typeToReviewTokens(predicate.bound_predicate.type));
      tokens.push({ Kind: TokenKind.Text, Value: ":" });
      tokens.push(...createGenericBoundTokens(predicate.bound_predicate.bounds));
    } else if (
      "lifetime_predicate" in predicate &&
      !shouldElideLifetime(predicate.lifetime_predicate.lifetime)
    ) {
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: predicate.lifetime_predicate.lifetime,
        HasSuffixSpace: false,
      });
      tokens.push({ Kind: TokenKind.Text, Value: ":" });
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: predicate.lifetime_predicate.outlives.toString(),
        HasSuffixSpace: false,
      });
    } else if ("eq_predicate" in predicate) {
      tokens.push(...typeToReviewTokens(predicate.eq_predicate.lhs));
      tokens.push({ Kind: TokenKind.Text, Value: "=" });
      if ("type" in predicate.eq_predicate.rhs) {
        tokens.push(...typeToReviewTokens(predicate.eq_predicate.rhs.type));
      } else {
        tokens.push({ Kind: TokenKind.Text, Value: "unknown" }); // Unknown is a placeholder for Const
      }
    }

    // Add comma between predicates
    if (tokens.length > 0 && index < wherePredicates.length - 1) {
      tokens.push({ Kind: TokenKind.Text, Value: "," });
    }

    return tokens;
  });
  if (result.length > 0 && result[result.length - 1].Value === ",") {
    result.pop();
  }
  return result;
}

export function createGenericBoundTokens(bounds: GenericBound[]): ReviewToken[] {
  return bounds.flatMap((bound, i) => {
    const tokens: ReviewToken[] = [];
    if (i > 0) {
      tokens.push({ Kind: TokenKind.Text, Value: " + ", HasSuffixSpace: false });
    }
    if ("trait_bound" in bound && bound.trait_bound?.trait) {
      tokens.push(
        {
          Kind: TokenKind.TypeName,
          Value: getPath(bound.trait_bound.trait),
          NavigateToId: bound.trait_bound.trait.id.toString(),
          HasSuffixSpace: false,
        },
        ...processGenericArgs(bound.trait_bound.trait.args),
      );
    } else if ("outlives" in bound) {
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: bound.outlives,
        HasSuffixSpace: false,
      });
    } else if ("use" in bound) {
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: bound.use.toString(),
        HasSuffixSpace: false,
      });
    }
    return tokens;
  });
}

export function processGenericArgs(args: GenericArgs): ReviewToken[] {
  let result: ReviewToken[] = [];
  // Check if args is empty
  if (!args) return result;
  // Process generic arguments based on their type
  if ("angle_bracketed" in args) {
    // Filter valid args that have a type property
    const validArgs = args.angle_bracketed.args.filter(
      (arg) => typeof arg === "object" && "type" in arg,
    );

    if (validArgs.length > 0) {
      result.push({ Kind: TokenKind.Punctuation, Value: "<", HasSuffixSpace: false });

      // Process each argument and add comma between them
      validArgs.forEach((arg, index) => {
        // Add the argument tokens
        result.push(...typeToReviewTokens(arg.type));

        // Add comma after each argument except the last one
        if (index < validArgs.length - 1) {
          result.push({ Kind: TokenKind.Punctuation, Value: ",", HasSuffixSpace: true });
        }
      });

      result.push({ Kind: TokenKind.Punctuation, Value: ">", HasSuffixSpace: false });
    }

    if (args.angle_bracketed.constraints.length > 0) {
      result.push({ Kind: TokenKind.Punctuation, Value: "<", HasSuffixSpace: false });
      result.push(
        ...args.angle_bracketed.constraints.flatMap((c) => {
          // Process constraint name
          const tokens: ReviewToken[] = [{ Kind: TokenKind.TypeName, Value: c.name }];

          // Process constraint binding
          if (c.binding) {
            if ("equality" in c.binding) {
              tokens.push({ Kind: TokenKind.Punctuation, Value: "=", HasSuffixSpace: true });
              // Handle the term on the right side of the equality
              if ("type" in c.binding.equality) {
                tokens.push(...typeToReviewTokens(c.binding.equality.type));
              } else if ("constant" in c.binding.equality) {
                tokens.push({ Kind: TokenKind.Text, Value: c.binding.equality.constant.expr });
              }
            } else if ("constraint" in c.binding) {
              tokens.push({ Kind: TokenKind.Punctuation, Value: ":", HasSuffixSpace: true });
              tokens.push(...createGenericBoundTokens(c.binding.constraint));
            }
          }

          // Process constraint arguments if present
          if (c.args) {
            tokens.push(...processGenericArgs(c.args));
          }

          tokens.push({ Kind: TokenKind.Punctuation, Value: ",", HasSuffixSpace: true });
          return tokens;
        }),
      );
      result.pop(); // Remove the last comma
      result.push({ Kind: TokenKind.Punctuation, Value: ">", HasSuffixSpace: false });
    }
  } else if ("parenthesized" in args) {
    // Add opening parenthesis
    result.push({ Kind: TokenKind.Punctuation, Value: "(", HasSuffixSpace: false });

    // Process input types
    args.parenthesized.inputs.forEach((input, index) => {
      result.push(...typeToReviewTokens(input));
      if (index < args.parenthesized.inputs.length - 1) {
        result.push({ Kind: TokenKind.Punctuation, Value: ",", HasSuffixSpace: true });
      }
    });

    result.push({ Kind: TokenKind.Punctuation, Value: ")", HasSuffixSpace: false });

    // Process output type if present
    if (args.parenthesized.output) {
      result.push({ Kind: TokenKind.Punctuation, Value: " -> ", HasSuffixSpace: false });
      result.push(...typeToReviewTokens(args.parenthesized.output));
    }
  }
  return result;
}
