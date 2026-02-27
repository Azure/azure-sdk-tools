import { enumTokenGenerator } from "./enum";
import { enumMemberTokenGenerator } from "./enumMember";
import { ReviewToken, ReviewLine } from "../models";
import { ApiItem } from "@microsoft/api-extractor-model";
import { functionTokenGenerator } from "./function";
import { interfaceTokenGenerator } from "./interfaces";
import { classTokenGenerator } from "./class";
import { constructorTokenGenerator } from "./constructor";
import { callableSignatureTokenGenerator } from "./callableSignature";
import { methodTokenGenerator } from "./method";
import { propertyTokenGenerator } from "./property";
import { typeAliasTokenGenerator } from "./typeAlias";
import { variableTokenGenerator } from "./variable";
import { namespaceTokenGenerator } from "./namespace";
import { indexSignatureTokenGenerator } from "./indexSignature";

export interface GeneratorResult {
  tokens: ReviewToken[];
  children?: ReviewLine[];
}

export interface TokenGenerator<T extends ApiItem = ApiItem> {
  isValid(item: ApiItem): item is T;
  generate(item: T, deprecated?: boolean): GeneratorResult;
}

export const generators: TokenGenerator[] = [
  enumTokenGenerator,
  enumMemberTokenGenerator,
  classTokenGenerator,
  functionTokenGenerator,
  interfaceTokenGenerator,
  constructorTokenGenerator,
  callableSignatureTokenGenerator,
  methodTokenGenerator,
  propertyTokenGenerator,
  typeAliasTokenGenerator,
  variableTokenGenerator,
  namespaceTokenGenerator,
  indexSignatureTokenGenerator,
];
