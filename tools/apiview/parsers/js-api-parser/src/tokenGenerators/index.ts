import { enumTokenGenerator } from "./enum";
import { ReviewToken, ReviewLine } from "../models";
import { ApiItem } from "@microsoft/api-extractor-model";
import { functionTokenGenerator } from "./function";
import { interfaceTokenGenerator } from "./interfaces";
import { classTokenGenerator } from "./class";
import { methodTokenGenerator } from "./method";
import { propertyTokenGenerator } from "./property";
import { typeAliasTokenGenerator } from "./typeAlias";
import { variableTokenGenerator } from "./variable";

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
  classTokenGenerator,
  functionTokenGenerator,
  interfaceTokenGenerator,
  methodTokenGenerator,
  propertyTokenGenerator,
  typeAliasTokenGenerator,
  variableTokenGenerator,
];
