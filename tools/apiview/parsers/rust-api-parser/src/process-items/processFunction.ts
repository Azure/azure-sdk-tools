import { ReviewLine, ReviewToken, TokenKind } from "../utils/apiview-models";
import { Item, GenericParamDef, WherePredicate, GenericBound, Type } from "../utils/rustdoc-json-types/jsonTypes";
import { processStructField } from "./processStructField";

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processFunction(item: Item, reviewLines: ReviewLine[]) {
    if (!(typeof item.inner === 'object' && 'function' in item.inner)) return;

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub fn'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.MemberName,
        Value: item.name || "null",
        HasSuffixSpace: false,
        RenderClasses: [
            "method"
        ],
        NavigateToId: item.id.toString(),
        NavigationDisplayName: item.name || undefined
    });

    const params = item.inner.function.generics.params;
    // Add generics if present
    const genericsParamTokens =
        createGenericsParamsTokens(params)

    reviewLine.Tokens.push(...genericsParamTokens);
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: '(',
        HasSuffixSpace: false,
        HasPrefixSpace: false
    });

    // Add function parameters
    if (item.inner.function.sig.inputs.length > 0) {
        item.inner.function.sig.inputs.forEach((input: [string, Type], index: number) => {
            if (index > 0) {
                reviewLine.Tokens.push({
                    Kind: TokenKind.Punctuation,
                    Value: ', ',
                    HasSuffixSpace: false
                });
            }

            reviewLine.Tokens.push({
                Kind: TokenKind.StringLiteral,
                Value: input[0],
                HasSuffixSpace: false
            });

            reviewLine.Tokens.push({
                Kind: TokenKind.Punctuation,
                Value: ': ',
                HasSuffixSpace: false
            });
            const token = processStructField(input[1])
            reviewLine.Tokens.push(token);
        });
    }

    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: ')',
        HasPrefixSpace: false,
    });

    // Add return type if present
    if (item.inner.function.sig.output) {
        reviewLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: '->'
        });
        reviewLine.Tokens.push({
            Kind: TokenKind.TypeName,
            Value: "unknown" // Fix this to present better value
        });
    }

    const wherePredicates = item.inner.function.generics.where_predicates;
    const wherePredicateTokens = wherePredicates.flatMap((predicate: WherePredicate, index: number) => {
        const tokens: ReviewToken[] = [];
        if (index > 0) {
            tokens.push({ Kind: TokenKind.Text, Value: ", ", HasSuffixSpace: false });
        }

        if ('bound_predicate' in predicate) {
            const type = predicate.bound_predicate.type
            const typeName = typeof type === 'object' && 'generic' in type ? type.generic : 'unknown';
            tokens.push({
                Kind: TokenKind.TypeName,
                Value: typeName,
                HasSuffixSpace: false
            });
            tokens.push({ Kind: TokenKind.Text, Value: ": ", HasSuffixSpace: false });
            tokens.push(...createGenericBoundTokens(predicate.bound_predicate.bounds))
        }

        return tokens;
    });

    if (wherePredicateTokens.length > 0) {
        reviewLine.Tokens.push({ Kind: TokenKind.Keyword, Value: "where", HasSuffixSpace: true }, ...wherePredicateTokens);
    }

    reviewLines.push(reviewLine);
}

function createGenericsParamsTokens(
    params: GenericParamDef[]
): ReviewToken[] {
    return params.flatMap((param: GenericParamDef, index: number) => {
        const tokens: ReviewToken[] = [];

        if (index === 0) {
            tokens.push({ Kind: TokenKind.Text, Value: "<", HasSuffixSpace: false });
        } else {
            tokens.push({ Kind: TokenKind.Text, Value: ", ", HasSuffixSpace: false });
        }

        // Param name
        tokens.push({
            Kind: TokenKind.TypeName,
            Value: param.name,
            HasSuffixSpace: false
        });

        // Param bounds
        if ("type" in param.kind && param.kind.type.bounds && param.kind.type.bounds.length > 0) {
            tokens.push({ Kind: TokenKind.Text, Value: ": ", HasSuffixSpace: false });
            tokens.push(...createGenericBoundTokens(param.kind.type.bounds));
        }

        // Close generics if it's the last parameter
        if (index === params.length - 1) {
            tokens.push({ Kind: TokenKind.Text, Value: ">", HasSuffixSpace: false });
        }

        return tokens;
    })
}

function createGenericBoundTokens(bounds: GenericBound[]): ReviewToken[] {
    return bounds.flatMap((bound, index) => {
        const tokens: ReviewToken[] = [];
        if (index > 0) {
            tokens.push({ Kind: TokenKind.Text, Value: " + ", HasSuffixSpace: false });
        }
        if ("trait_bound" in bound && bound.trait_bound?.trait) {
            tokens.push({
                Kind: TokenKind.TypeName,
                Value: bound.trait_bound.trait.name,
                NavigateToId: bound.trait_bound.trait.id.toString(),
                HasSuffixSpace: false
            });
        }
        return tokens;
    });
}