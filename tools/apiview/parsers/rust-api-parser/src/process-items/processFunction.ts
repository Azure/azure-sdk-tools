import { ReviewLine, ReviewToken, TokenKind } from "../models/apiview-models";
import {
  Item,
  GenericParamDef,
  WherePredicate,
  GenericBound,
  Type,
} from "../../rustdoc-types/output/rustdoc-types";
import { processStructField } from "./processStructField";
import { createDocsReviewLine } from "./utils/generateDocReviewLine";
import { shouldElideLifetime } from "./utils/shouldElideLifeTime";
import { typeToString } from "./utils/typeToString";

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processFunction(item: Item) {
  if (!(typeof item.inner === "object" && "function" in item.inner)) return;
  const reviewLines: ReviewLine[] = [];
  if (item.docs) reviewLines.push(createDocsReviewLine(item));

  // Create the ReviewLine object
  const reviewLine: ReviewLine = {
    LineId: item.id.toString(),
    Tokens: [],
    Children: [],
  };

  reviewLine.Tokens.push({
    Kind: TokenKind.Keyword,
    Value: "pub fn",
  });
  reviewLine.Tokens.push({
    Kind: TokenKind.MemberName,
    Value: item.name || "null",
    HasSuffixSpace: false,
    RenderClasses: ["method"],
    NavigateToId: item.id.toString(),
    NavigationDisplayName: item.name || undefined,
  });

  const params = item.inner.function.generics.params;
  // Add generics if present
  const genericsParamTokens = createGenericsParamsTokens(params);

  reviewLine.Tokens.push(...genericsParamTokens);
  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: "(",
    HasSuffixSpace: false,
    HasPrefixSpace: false,
  });

  // Add function parameters
  if (item.inner.function.sig.inputs.length > 0) {
    item.inner.function.sig.inputs.forEach((input: [string, Type], index: number) => {
      if (index > 0) {
        reviewLine.Tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ", ",
          HasSuffixSpace: false,
        });
      }

      if (input[0] === "self") {
        reviewLine.Tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: input[0],
          HasSuffixSpace: false,
        });
      } else {
        reviewLine.Tokens.push({
          Kind: TokenKind.StringLiteral,
          Value: input[0],
          HasSuffixSpace: false,
        });

        reviewLine.Tokens.push({
          Kind: TokenKind.Punctuation,
          Value: ": ",
          HasSuffixSpace: false,
        });
        const token = processStructField(input[1]);
        reviewLine.Tokens.push(token);
      }
    });
  }

  reviewLine.Tokens.push({
    Kind: TokenKind.Punctuation,
    Value: ")",
    HasPrefixSpace: false,
  });

  // Add return type if present
  if (item.inner.function.sig.output) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "->",
    });
    reviewLine.Tokens.push({
      Kind: TokenKind.TypeName,
      Value: typeToString(item.inner.function.sig.output),
    });
  }

  const wherePredicates = item.inner.function.generics.where_predicates;
  const wherePredicateTokens = wherePredicates.flatMap(
    (predicate: WherePredicate, index: number) => {
      const tokens: ReviewToken[] = [];

      if (index > 0 && tokens.length > 0 && index < wherePredicates.length - 1) {
        tokens.push({ Kind: TokenKind.Text, Value: "," });
      }
      if ("bound_predicate" in predicate) {
        tokens.push({
          Kind: TokenKind.TypeName,
          Value: typeToString(predicate.bound_predicate.type),
          HasSuffixSpace: false,
        });
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
        tokens.push({
          Kind: TokenKind.TypeName,
          Value: typeToString(predicate.eq_predicate.lhs),
        });
        tokens.push({ Kind: TokenKind.Text, Value: "=" });
        tokens.push({
          Kind: TokenKind.TypeName,
          Value:
            "type" in predicate.eq_predicate.rhs
              ? typeToString(predicate.eq_predicate.rhs.type)
              : "unknown", // Unknown is a placeholder for Const
          HasSuffixSpace: false,
        });
      }
      return tokens;
    },
  );

  if (wherePredicateTokens.length > 0) {
    reviewLine.Tokens.push(
      { Kind: TokenKind.Keyword, Value: "where", HasSuffixSpace: true },
      ...wherePredicateTokens,
    );
  }

  if (item.inner.function.has_body) {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: "{}",
      HasSuffixSpace: false,
    });
  } else {
    reviewLine.Tokens.push({
      Kind: TokenKind.Punctuation,
      Value: ";",
      HasSuffixSpace: false,
    });
  }
  reviewLines.push(reviewLine);
  return reviewLines;
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

function createGenericBoundTokens(bounds: GenericBound[]): ReviewToken[] {
  return bounds.flatMap((bound, index) => {
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
