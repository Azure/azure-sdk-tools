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

export function processGenerics(generics: Generics): {
  params: ReviewToken[];
  wherePredicates: ReviewToken[];
} {
  // Process generic parameters
  const paramsTokens = createGenericsParamsTokens(generics.params);

  // Process where predicates if present
  const wherePredicates = generics.where_predicates;
  const whereTokens: ReviewToken[] = [];
  if (wherePredicates.length > 0) {
    whereTokens.push(
      { Kind: TokenKind.Keyword, Value: "where", HasSuffixSpace: true, HasPrefixSpace: true },
      ...createWherePredicatesTokens(wherePredicates),
    );
  }

  return { params: paramsTokens, wherePredicates: whereTokens };
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
    } else {
      if (index != 0 && tokens.length > 0) {
        tokens.push({ Kind: TokenKind.Text, Value: "," });
      }
    }

    return tokens;
  });
}

function createWherePredicatesTokens(wherePredicates: WherePredicate[]): ReviewToken[] {
  return wherePredicates.flatMap((predicate: WherePredicate, index: number) => {
    const tokens: ReviewToken[] = [];

    if (index > 0 && tokens.length > 0 && index < wherePredicates.length - 1) {
      tokens.push({ Kind: TokenKind.Text, Value: "," });
    }

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
    return tokens;
  });
}

export function createGenericBoundTokens(bounds: GenericBound[]): ReviewToken[] {
  return bounds.flatMap((bound) => {
    const tokens: ReviewToken[] = [];
    if (tokens.length > 0) {
      tokens.push({ Kind: TokenKind.Text, Value: " + ", HasSuffixSpace: false });
    }
    if ("trait_bound" in bound && bound.trait_bound?.trait) {
      tokens.push({
        Kind: TokenKind.TypeName,
        Value: bound.trait_bound.trait.name,
        NavigateToId: bound.trait_bound.trait.id.toString(),
        HasSuffixSpace: false,
      });
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
  // Process generic arguments based on their type
  if ("angle_bracketed" in args) {
    const processedArgs = args.angle_bracketed.args
      .filter((arg) => typeof arg === "object" && "type" in arg)
      .map((arg) => typeToReviewTokens(arg.type))
      .flatMap((a) => a);

    if (processedArgs.length > 0) {
      result.push({ Kind: TokenKind.Punctuation, Value: "<", HasSuffixSpace: false });
      result.push(...processedArgs);
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
