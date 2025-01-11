import { ReviewLine, TokenKind } from "./apiview-models/models";
import { ApiJson, Item } from "./interfaces";

const processedItems = new Set<string>();

/**
 * Processes an item from the API JSON and returns a ReviewLine object.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The item to process.
 * @param {string} [indent=''] - The indentation to use for the item's documentation.
 * @returns {ReviewLine | null} The ReviewLine object or null if the item is not processed.
 */
export function processItem(apiJson: ApiJson, item: Item, indent: string = ''): ReviewLine | null {
    // Check if the item has already been processed
    if (item.name && processedItems.has(item.name)) {
        return null;
    }
    item.name && processedItems.add(item.name);

    // Create the ReviewLine object
    const reviewLine: ReviewLine = {
        LineId: item.id.toString(),
        Tokens: [],
        Children: []
    };

    // Add documentation token if available
    // if (item.docs) {
    // TODO: Push to children and add link with "related to line"
    // reviewLine.Tokens.push({
    //     Kind: TokenKind.Comment,
    //     Value: `/// ${item.docs}`,
    //     IsDocumentation: true
    // });
    // }

    if (item.inner.module) {
        processModule(apiJson, item, reviewLine);
    } else if (item.inner.function) {
        processFunction(item, reviewLine);
    } else if (item.inner.struct) {
        processStruct(apiJson, item, reviewLine);
    } else if (item.inner.trait) {
        processTrait(apiJson, item, reviewLine);
    }

    return reviewLine;
}

/**
 * Processes a module item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The module item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
function processModule(apiJson: ApiJson, item: Item, reviewLine: ReviewLine) {
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub mod'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null"
    });

    if (item.inner.module.items) {
        item.inner.module.items.forEach((childId: string) => {
            const childItem = apiJson.index[childId];
            const childReviewLine = processItem(apiJson, childItem);
            if (childReviewLine) {
                if (!reviewLine.Children) {
                    reviewLine.Children = [];
                }
                reviewLine.Children.push(childReviewLine);
            }
        });
    }
}
/**
 * Processes a function item and adds its documentation to the ReviewLine.
 *
 * @param {Item} item - The function item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
function processFunction(item: Item, reviewLine: ReviewLine) {
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub fn'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.MemberName,
        Value: item.name || "null"
    });

    // Add generics if present
    if (item.inner.function.generics.params.length > 0) {
        reviewLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: '<'
        });
        reviewLine.Tokens.push({
            Kind: TokenKind.TypeName,
            Value: item.inner.function.generics.params.map((param: any) => param.name).join(', ')
        });
        reviewLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: '>'
        });
    }

    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: '('
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
            }).join(', ')
        });
    }

    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: ')'
    });

    // Add return type if present
    if (item.inner.function.sig.output) {
        reviewLine.Tokens.push({
            Kind: TokenKind.Punctuation,
            Value: '->'
        });
        reviewLine.Tokens.push({
            Kind: TokenKind.TypeName,
            Value: item.inner.function.sig.output.primitive || item.inner.function.sig.output.resolved_path.name
        });
    }
}

/**
 * Processes a struct item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The struct item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
function processStruct(apiJson: ApiJson, item: Item, reviewLine: ReviewLine) {
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub struct'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null"
    });

    if (item.inner.struct.kind.plain.fields) {
        item.inner.struct.kind.plain.fields.forEach((fieldId: string) => {
            const fieldItem = apiJson.index[fieldId];
            if (fieldItem && fieldItem.inner.struct_field) {
                if (!reviewLine.Children) {
                    reviewLine.Children = [];
                }
                reviewLine.Children.push({
                    LineId: fieldItem.id.toString(),
                    Tokens: [
                        {
                            Kind: TokenKind.Keyword,
                            Value: 'pub'
                        },
                        {
                            Kind: TokenKind.MemberName,
                            Value: fieldItem.name || "null"
                        },
                        {
                            Kind: TokenKind.Punctuation,
                            Value: ':'
                        },
                        {
                            Kind: TokenKind.TypeName,
                            Value: fieldItem.inner.struct_field.primitive || fieldItem.inner.struct_field.resolved_path.name
                        }
                    ]
                });
            }
        });
    }
}

/**
 * Processes a trait item and adds its documentation to the ReviewLine.
 *
 * @param {ApiJson} apiJson - The API JSON object containing all items.
 * @param {Item} item - The trait item to process.
 * @param {ReviewLine} reviewLine - The ReviewLine object to update.
 */
function processTrait(apiJson: ApiJson, item: Item, reviewLine: ReviewLine) {
    reviewLine.Tokens.push({
        Kind: TokenKind.Keyword,
        Value: 'pub trait'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.TypeName,
        Value: item.name || "null"
    });

    if (item.inner.trait.items) {
        item.inner.trait.items.forEach((methodId: string) => {
            const methodItem = apiJson.index[methodId];
            if (methodItem.inner.function) {
                const methodReviewLine: ReviewLine = {
                    LineId: methodItem.id.toString(),
                    Tokens: [
                        {
                            Kind: TokenKind.Keyword,
                            Value: 'fn'
                        },
                        {
                            Kind: TokenKind.MemberName,
                            Value: methodItem.name || "null"
                        },
                        {
                            Kind: TokenKind.Punctuation,
                            Value: '('
                        },
                        {
                            Kind: TokenKind.Punctuation,
                            Value: ')'
                        }
                    ],
                    Children: []
                };

                // Add function signature tokens
                if (methodItem.inner.function.generics.params.length > 0) {
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.Punctuation,
                        Value: '<'
                    });
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.TypeName,
                        Value: methodItem.inner.function.generics.params.map((param: any) => param.name).join(', ')
                    });
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.Punctuation,
                        Value: '>'
                    });
                }

                if (methodItem.inner.function.sig.inputs.length > 0) {
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.Punctuation,
                        Value: '('
                    });
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.TypeName,
                        Value: methodItem.inner.function.sig.inputs.map((input: any) => {
                            if (input[1].primitive) {
                                return `${input[0]}: ${input[1].primitive}`;
                            } else if (input[1].resolved_path) {
                                return `${input[0]}: ${input[1].resolved_path.name}`;
                            } else if (input[1].borrowed_ref) {
                                return `${input[0]}: &${input[1].borrowed_ref.type.generic}`;
                            } else {
                                return `${input[0]}: unknown`;
                            }
                        }).join(', ')
                    });
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.Punctuation,
                        Value: ')'
                    });
                }

                if (methodItem.inner.function.sig.output) {
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.Punctuation,
                        Value: '->'
                    });
                    methodReviewLine.Tokens.push({
                        Kind: TokenKind.TypeName,
                        Value: methodItem.inner.function.sig.output.primitive || methodItem.inner.function.sig.output.resolved_path.name
                    });
                }

                if (!reviewLine.Children) {
                    reviewLine.Children = [];
                }
                reviewLine.Children.push(methodReviewLine);
            }
        });
    }
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: '{'
    });
    reviewLine.Tokens.push({
        Kind: TokenKind.Punctuation,
        Value: '}'
    });
}