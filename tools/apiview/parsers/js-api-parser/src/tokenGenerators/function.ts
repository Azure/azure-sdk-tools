import { ApiFunction, ApiItem, ApiItemKind, ExcerptTokenKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";

function isValid(item: ApiItem): item is ApiFunction {
    return item.kind === ApiItemKind.Function;
}

function generate(item: ApiFunction, deprecated?: boolean): ReviewToken[] {
    const tokens: ReviewToken[] = [];   
    if (item.kind !== ApiItemKind.Function) {
        throw new Error(`Invalid item ${item.displayName} of kind ${item.kind} for Function token generator.`);
    }
    
    // Add export keyword
    tokens.push({Kind: TokenKind.Keyword, Value: "export", HasSuffixSpace: true, IsDeprecated: deprecated});
    
    // Add function keyword
    tokens.push({Kind: TokenKind.Keyword, Value: "function", HasSuffixSpace: true, IsDeprecated: deprecated});
    
    // Add function name
    tokens.push({Kind: TokenKind.MemberName, Value: item.displayName, IsDeprecated: deprecated});
    
    // Process parameters and return type from excerptTokens
    for (const excerpt of item.excerptTokens) {
        if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
            tokens.push({
                Kind: TokenKind.TypeName,
                Value: excerpt.text,
                NavigateToId: excerpt.canonicalReference.toString(),
                IsDeprecated: deprecated
            });
        } else {
            // For other tokens (punctuation, parameter names, etc.)
            const text = excerpt.text.trim();
            if (text) {
                tokens.push({
                    Kind: TokenKind.Text,
                    Value: text,
                    IsDeprecated: deprecated
                });
            }
        }
    }
       
    return tokens;
}

export const functionTokenGenerator: TokenGenerator<ApiFunction> = {
  isValid,
  generate,
};