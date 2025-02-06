import { Type } from "../models/rustdoc-json-types";
import { ReviewToken, TokenKind } from "../models/apiview-models";
import { typeToString } from "./utils/typeToString";

export function processStructField(struct_field: Type): ReviewToken {
  // Add the struct field type
  const reviewToken: ReviewToken = {
    Kind: TokenKind.TypeName,
    Value: typeToString(struct_field),
    HasSuffixSpace: false,
  };
  return reviewToken;
}
