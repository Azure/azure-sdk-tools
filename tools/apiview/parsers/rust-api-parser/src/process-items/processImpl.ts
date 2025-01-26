import { ReviewLine, TokenKind, ReviewToken } from "../utils/apiview-models";
import { Item, Crate, Impl, Struct } from "../utils/rustdoc-json-types/jsonTypes";
import { processItem } from "./processItem";

export function processImpl(item: Omit<Item, "inner"> & { inner: { struct: Struct; } }, apiJson: Crate, reviewLine: ReviewLine) {
    type ImplItem = Omit<Item, "inner"> & { inner: { impl: Impl } };

    const traitImpls = (impls: number[], apiJson: Crate) =>
        impls.map(implId => apiJson.index[implId] as ImplItem)
            .filter((implItem) => typeof implItem?.inner == "object" && "impl" in implItem?.inner && implItem.inner.impl.blanket_impl === null && implItem.inner.impl.trait)
            .map<ReviewToken>(implItem => ({
                Kind: TokenKind.TypeName,
                Value: implItem.inner.impl.trait.name,
                RenderClasses: [
                    "trait"
                ],
                // NavigateToId: implItem.inner.impl.trait.id.toString(),
                // NavigationDisplayName: implItem.inner.impl.trait.name || undefined,
                HasSuffixSpace: false
            }));

    const addTokens = (tokens: ReviewToken[]) => {
        reviewLine.Tokens.push({ Kind: TokenKind.Punctuation, Value: '#[', HasSuffixSpace: false });
        reviewLine.Tokens.push({ Kind: TokenKind.Keyword, Value: "derive", HasSuffixSpace: false });
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

    const traits = traitImpls(item.inner.struct.impls, apiJson);
    if (traits.length) addTokens(traits);

    const nonTraitImpls = (impls: number[], apiJson: Crate): void =>
        impls.map(implId => apiJson.index[implId] as ImplItem)
            .filter(implItem => implItem?.inner && "impl" in implItem.inner && implItem.inner.impl.blanket_impl === null && implItem.inner.impl.trait === null)
            .forEach(implItem => {
                const items = implItem.inner.impl.items;
                if (!reviewLine.Children.length) reviewLine.Children = [];
                items.forEach(item => {
                    processItem(apiJson, apiJson.index[item], reviewLine.Children);
                });
            });

    nonTraitImpls(item.inner.struct.impls, apiJson);
}