import { ReviewLine, TokenKind, ReviewToken } from "../utils/apiview-models";
import { Item, Crate, Impl, Struct } from "../utils/rustdoc-json-types/jsonTypes";

export function processImpl(item: Omit<Item, "inner"> & { inner: { struct: Struct; } }, apiJson: Crate, reviewLine: ReviewLine) {
    type ImplItem = Omit<Item, "inner"> & { inner: { impl: Impl } };

    const derivedTokens = (impls: number[], apiJson: Crate, filterCallback: (implItem: ImplItem) => boolean) =>
        impls.map(implId => apiJson.index[implId] as ImplItem)
            .filter((implItem) => typeof implItem?.inner == "object" && "impl" in implItem?.inner && implItem.inner.impl.blanket_impl === null)
            .filter(filterCallback)
            .map(implItem => ({
                Kind: TokenKind.TypeName, Value: typeof implItem?.inner == "object" && "impl" in implItem?.inner ? implItem.inner.impl.trait.name : "unknown",
                RenderClasses: [
                    "trait"
                ],
                NavigateToId: implItem.inner.impl.trait.id.toString(),
                NavigationDisplayName: implItem.inner.impl.trait.name || undefined,
                HasSuffixSpace: false
            }));

    const addTokens = (tokens: ReviewToken[], kind: string) => {
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: '#[', HasSuffixSpace: false });
        reviewLine.Tokens.push({ Kind: TokenKind.Keyword, Value: kind, HasSuffixSpace: false });
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: '(', HasSuffixSpace: false });
        tokens.forEach((token, index) => {
            reviewLine.Tokens.push(token);
            if (index < tokens.length - 1) {
                reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: ',' });
            }
        });
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: ')]' });
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: '\n', HasSuffixSpace: false });
    };

    const automatically_derived = derivedTokens(item.inner.struct.impls, apiJson, implItem => implItem.attrs.includes("#[automatically_derived]"));
    const custom_derived = derivedTokens(item.inner.struct.impls, apiJson, implItem => !implItem.attrs.includes("#[automatically_derived]"));

    if (automatically_derived.length) addTokens(automatically_derived, 'derive');
    if (custom_derived.length) addTokens(custom_derived, 'custom_derive');
}