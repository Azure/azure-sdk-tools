import { ReviewLine, ReviewToken, TokenKind } from "../apiview-models";
import { Item, GenericParamDef, WherePredicate, GenericBound } from "../rustdoc-json-types/jsonTypes";

/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
export function processFunction(item: Item, reviewLines: ReviewLine[]) {
    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    if (!(typeof item.inner === 'object' && 'function' in item.inner)) return;

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
        reviewLine.Tokens.push({
            Kind: TokenKind.TypeName,
            Value: item.inner.function.sig.inputs.map((input: any) => {
                if (input[1].primitive) {
                    return `${input[0]}: ${input[1].primitive}`;
                } else if (input[1].resolved_path) {
                    return `${input[0]}: ${input[1].resolved_path.name}`;
                } else if (input[1].borrowed_ref) {
                    return `${input[0]}: &${input[1].borrowed_ref.type.generic}`;
                } else {
                    return `${input[0]}: unknown`;
                }
            }).join(', '),
            HasSuffixSpace: false
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