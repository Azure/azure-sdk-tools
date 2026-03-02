import { describe, expect, it } from "vitest";
import { typeAliasTokenGenerator } from "../../src/tokenGenerators/typeAlias";
import {
  ApiTypeAlias,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { TokenKind, ReviewToken } from "../../src/models";

// Helper function to create a mock ApiTypeAlias with all required properties
function createMockTypeAlias(
  displayName: string,
  typeExcerptText: string,
  excerptTokens: ExcerptToken[],
  typeParameters?: TypeParameter[],
): ApiTypeAlias {
  const mock: any = {
    kind: ApiItemKind.TypeAlias,
    displayName,
    excerptTokens,
    typeParameters: typeParameters || [],
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!${displayName}:type`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    // Required properties from ApiDeclaredItem
    fileUrlPath: undefined,
    excerpt: {
      text: excerptTokens.map((t) => t.text).join(""),
      tokenRange: { startIndex: 0, endIndex: excerptTokens.length },
      tokens: excerptTokens,
    },
    typeExcerpt: {
      text: typeExcerptText,
      spannedTokens: [{ kind: ExcerptTokenKind.Content, text: typeExcerptText }],
    },
    // Additional required properties
    releaseTag: undefined,
    tsdocComment: undefined,
  };
  return mock as ApiTypeAlias;
}

// Helper to create basic excerpt tokens
function createBasicExcerptTokens(typeName: string, typeValue: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export " },
    { kind: ExcerptTokenKind.Content, text: "type " },
    { kind: ExcerptTokenKind.Content, text: typeName },
    { kind: ExcerptTokenKind.Content, text: " = " },
    { kind: ExcerptTokenKind.Content, text: typeValue },
  ] as ExcerptToken[];
}

// Helper to create excerpt tokens with default export
function createDefaultExportExcerptTokens(typeName: string, typeValue: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export default " },
    { kind: ExcerptTokenKind.Content, text: "type " },
    { kind: ExcerptTokenKind.Content, text: typeName },
    { kind: ExcerptTokenKind.Content, text: " = " },
    { kind: ExcerptTokenKind.Content, text: typeValue },
  ] as ExcerptToken[];
}

// Helper to create a mock TypeParameter
function createMockTypeParameter(
  name: string,
  constraintText?: string,
  defaultTypeText?: string,
): TypeParameter {
  const constraint = constraintText
    ? {
        text: constraintText,
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: constraintText }],
      }
    : { text: "", spannedTokens: [] };

  const defaultType = defaultTypeText
    ? {
        text: defaultTypeText,
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: defaultTypeText }],
      }
    : { text: "", spannedTokens: [] };

  return {
    name,
    constraintExcerpt: constraint,
    defaultTypeExcerpt: defaultType,
  } as unknown as TypeParameter;
}

describe("typeAliasTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for type alias items", () => {
      const mockTypeAlias = {
        kind: ApiItemKind.TypeAlias,
        displayName: "TestType",
      } as ApiTypeAlias;

      expect(typeAliasTokenGenerator.isValid(mockTypeAlias)).toBe(true);
    });

    it("returns false for non-type-alias items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as ApiItem;

      expect(typeAliasTokenGenerator.isValid(mockEnum)).toBe(false);
    });

    it("returns false for interface items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiItem;

      expect(typeAliasTokenGenerator.isValid(mockInterface)).toBe(false);
    });

    it("returns false for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiItem;

      expect(typeAliasTokenGenerator.isValid(mockClass)).toBe(false);
    });

    it("returns false for function items", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "testFunction",
      } as ApiItem;

      expect(typeAliasTokenGenerator.isValid(mockFunction)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a simple type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "SimpleType",
        "string",
        createBasicExcerptTokens("SimpleType", "string"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type SimpleType = string;
      expect(tokens.length).toBeGreaterThanOrEqual(5);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "type",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "SimpleType",
        IsDeprecated: false,
        NavigateToId: "@test!SimpleType:type",
        NavigationDisplayName: "SimpleType",
        RenderClasses: ["type"],
      });

      // Check for equals sign
      const equalsToken = tokens.find((t) => t.Value === "=");
      expect(equalsToken).toBeDefined();
      expect(equalsToken?.HasPrefixSpace).toBe(true);
      expect(equalsToken?.HasSuffixSpace).toBe(true);
    });

    it("generates correct tokens for a deprecated type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "DeprecatedType",
        "string",
        createBasicExcerptTokens("DeprecatedType", "string"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, true);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "type",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DeprecatedType",
        IsDeprecated: true,
      });
    });

    it("generates correct tokens for a default exported type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "DefaultType",
        "string",
        createDefaultExportExcerptTokens("DefaultType", "string"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "default",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "type",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[3]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DefaultType",
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for a type alias with a single type parameter", () => {
      const typeParam = createMockTypeParameter("T");
      const mockTypeAlias = createMockTypeAlias(
        "GenericType",
        "Array<T>",
        createBasicExcerptTokens("GenericType", "Array<T>"),
        [typeParam],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type GenericType<T> = Array<T>;
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      expect(lessThanIndex).toBeGreaterThan(-1);
      expect(greaterThanIndex).toBeGreaterThan(lessThanIndex);

      // Check for type parameter name
      const typeParamToken = tokens[lessThanIndex + 1];
      expect(typeParamToken).toEqual({
        Kind: TokenKind.TypeName,
        Value: "T",
        HasSuffixSpace: false,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for a type alias with multiple type parameters", () => {
      const typeParams = [createMockTypeParameter("K"), createMockTypeParameter("V")];
      const mockTypeAlias = createMockTypeAlias(
        "MapType",
        "Map<K, V>",
        createBasicExcerptTokens("MapType", "Map<K, V>"),
        typeParams,
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type MapType<K, V> = Map<K, V>;
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      expect(lessThanIndex).toBeGreaterThan(-1);
      expect(greaterThanIndex).toBeGreaterThan(lessThanIndex);

      // Check that we have two type parameters separated by commas
      const paramSection = tokens.slice(lessThanIndex + 1, greaterThanIndex);
      const typeParamNames = paramSection
        .filter((t) => t.Kind === TokenKind.TypeName)
        .map((t) => t.Value);

      expect(typeParamNames).toEqual(["K", "V"]);

      // Check for comma
      const commas = paramSection.filter((t) => t.Value === ",");
      expect(commas).toHaveLength(1);
    });

    it("generates correct tokens for a type alias with a constrained type parameter", () => {
      const typeParam = createMockTypeParameter("T", " object");
      const mockTypeAlias = createMockTypeAlias(
        "ConstrainedType",
        "T & { id: string }",
        createBasicExcerptTokens("ConstrainedType", "T & { id: string }"),
        [typeParam],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type ConstrainedType<T extends object> = T & { id: string };
      const extendsKeywordIndex = tokens.findIndex(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );

      expect(extendsKeywordIndex).toBeGreaterThan(-1);
      expect(tokens[extendsKeywordIndex]).toMatchObject({
        Kind: TokenKind.Keyword,
        Value: "extends",
        HasPrefixSpace: true,
        HasSuffixSpace: true,
      });
    });

    it("generates correct tokens for a type alias with default type parameter", () => {
      const typeParam = createMockTypeParameter("T", undefined, "string");
      const mockTypeAlias = createMockTypeAlias(
        "DefaultParamType",
        "Array<T>",
        createBasicExcerptTokens("DefaultParamType", "Array<T>"),
        [typeParam],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type DefaultParamType<T = string> = Array<T>;
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      const paramSection = tokens.slice(lessThanIndex + 1, greaterThanIndex);
      const equalsSigns = paramSection.filter((t) => t.Value === "=");

      expect(equalsSigns).toHaveLength(1);
    });

    it("generates correct tokens for a union type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "UnionType",
        "string | number | boolean",
        createBasicExcerptTokens("UnionType", "string | number | boolean"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type UnionType = string | number | boolean;
      // Check for pipe operators
      const pipeTokens = tokens.filter((t) => t.Value === "|");
      expect(pipeTokens.length).toBe(2);

      // Verify pipes have correct spacing
      pipeTokens.forEach((pipe) => {
        expect(pipe.HasPrefixSpace).toBe(true);
        expect(pipe.HasSuffixSpace).toBe(true);
      });
    });

    it("generates correct multi-line structure for a union of type literals", () => {
      const typeText =
        "{ startTime: Date; endTime: Date; } | { startTime: Date; duration: string; } | { duration: string; endTime: Date; } | { duration: string; }";
      const mockTypeAlias = createMockTypeAlias(
        "QueryTimeInterval",
        typeText,
        createBasicExcerptTokens("QueryTimeInterval", typeText),
      );

      const result = typeAliasTokenGenerator.generate(mockTypeAlias, false);
      const { tokens, children } = result;

      // Parent line should have: export type QueryTimeInterval = {
      // (only ONE opening brace on the parent line)
      const openBraces = tokens.filter((t) => t.Value === "{");
      expect(openBraces).toHaveLength(1);

      // Should have children (multi-line structure)
      expect(children).toBeDefined();
      expect(children!.length).toBeGreaterThan(0);

      // Children should contain "} | {" separator lines
      const allChildTokenValues = children!.map((c) => c.Tokens.map((t) => t.Value).join(""));
      const separatorLines = allChildTokenValues.filter((v) => v.includes("}") && v.includes("|") && v.includes("{"));
      expect(separatorLines).toHaveLength(3); // 3 separators for 4 type literals

      // Last child should be a closing line with "}" and ";"
      const lastChild = children![children!.length - 1];
      const lastValues = lastChild.Tokens.map((t) => t.Value);
      expect(lastValues).toContain("}");
      expect(lastValues).toContain(";");
      expect(lastChild.IsContextEndLine).toBe(true);

      // Member lines should exist (e.g., startTime, endTime, duration)
      const memberValues = children!.flatMap((c) => c.Tokens.map((t) => t.Value));
      expect(memberValues).toContain("startTime");
      expect(memberValues).toContain("endTime");
      expect(memberValues).toContain("duration");
      expect(memberValues).toContain("Date");
      expect(memberValues).toContain("string");
    });

    it("generates correct tokens for an intersection type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "IntersectionType",
        "TypeA & TypeB & TypeC",
        createBasicExcerptTokens("IntersectionType", "TypeA & TypeB & TypeC"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type IntersectionType = TypeA & TypeB & TypeC;
      // Check for ampersand operators
      const ampTokens = tokens.filter((t) => t.Value === "&");
      expect(ampTokens.length).toBe(2);

      // Verify ampersands have correct spacing
      ampTokens.forEach((amp) => {
        expect(amp.HasPrefixSpace).toBe(true);
        expect(amp.HasSuffixSpace).toBe(true);
      });
    });

    it("generates full method signatures inside intersection type literals", () => {
      const typeText =
        'Pick<BlobClient, "abortCopyFromURL" | "getProperties"> & { startCopyFromURL(copySource: string, options?: BlobStartCopyFromURLOptions): Promise<BlobBeginCopyFromURLResponse>; }';

      const mockTypeAlias = createMockTypeAlias(
        "CopyPollerBlobClient",
        typeText,
        createBasicExcerptTokens("CopyPollerBlobClient", typeText),
      );

      const result = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const methodLine = result.children?.find((line) =>
        line.Tokens.some((token) => token.Value === "startCopyFromURL"),
      );

      expect(methodLine).toBeDefined();

      const methodValues = methodLine!.Tokens.map((token) => token.Value);
      expect(methodValues).toContain("startCopyFromURL");
      expect(methodValues).toContain("(");
      expect(methodValues).toContain("copySource");
      expect(methodValues).toContain("string");
      expect(methodValues).toContain("options");
      expect(methodValues).toContain("?");
      expect(methodValues).toContain("BlobStartCopyFromURLOptions");
      expect(methodValues).toContain("Promise");
      expect(methodValues).toContain("BlobBeginCopyFromURLResponse");
      expect(methodValues).toContain(";");
    });
    it("generates correct tokens for a type literal alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "ObjectType",
        "{ name: string; age: number; }",
        createBasicExcerptTokens("ObjectType", "{ name: string; age: number; }"),
      );

      const result = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type ObjectType = { name: string; age: number; };
      // Should have children for inline type literal
      expect(result.tokens.length).toBeGreaterThan(0);

      // Check for opening brace
      const openBrace = result.tokens.find((t) => t.Value === "{");
      expect(openBrace).toBeDefined();
    });

    it("generates correct tokens for an array type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "ArrayType",
        "string[]",
        createBasicExcerptTokens("ArrayType", "string[]"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type ArrayType = string[];
      expect(tokens.length).toBeGreaterThan(0);
    });

    it("generates correct tokens for a tuple type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "TupleType",
        "[string, number, boolean]",
        createBasicExcerptTokens("TupleType", "[string, number, boolean]"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type TupleType = [string, number, boolean];
      expect(tokens.length).toBeGreaterThan(0);
    });

    it("generates correct tokens for a function type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "FunctionType",
        "(arg: string) => void",
        createBasicExcerptTokens("FunctionType", "(arg: string) => void"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type FunctionType = (arg: string) => void;
      expect(tokens.length).toBeGreaterThan(0);

      // The function type is rendered as a single token or parsed by the type parser
      // Verify the type content is present somewhere in the tokens
      const hasArrow = tokens.some((t) => t.Value.includes("=>"));
      expect(hasArrow).toBe(true);
    });

    it("generates correct tokens for a conditional type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "ConditionalType",
        "T extends string ? true : false",
        createBasicExcerptTokens("ConditionalType", "T extends string ? true : false"),
        [createMockTypeParameter("T")],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type ConditionalType<T> = T extends string ? true : false;
      expect(tokens.length).toBeGreaterThan(0);
    });

    it("generates correct tokens for a keyof type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "KeysType",
        "keyof SomeInterface",
        createBasicExcerptTokens("KeysType", "keyof SomeInterface"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type KeysType = keyof SomeInterface;
      expect(tokens.length).toBeGreaterThan(0);
    });

    it("generates correct tokens for a mapped type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "MappedType",
        "{ [K in keyof T]: T[K] }",
        createBasicExcerptTokens("MappedType", "{ [K in keyof T]: T[K] }"),
        [createMockTypeParameter("T")],
      );

      const result = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type MappedType<T> = { [K in keyof T]: T[K] };
      expect(result.tokens.length).toBeGreaterThan(0);
    });

    it("throws an error for invalid item kind", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
        excerptTokens: [],
      } as any;

      expect(() => {
        typeAliasTokenGenerator.generate(mockClass, false);
      }).toThrow("Invalid item TestClass of kind Class for TypeAlias token generator.");
    });

    it("preserves deprecated state across all tokens", () => {
      const typeParam = createMockTypeParameter("T");
      const mockTypeAlias = createMockTypeAlias(
        "DeprecatedComplexType",
        "T | null",
        createBasicExcerptTokens("DeprecatedComplexType", "T | null"),
        [typeParam],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, true);

      // All tokens generated by the generator should have IsDeprecated: true
      const generatorTokens = tokens.slice(0, 4); // export, type, name, <
      generatorTokens.forEach((token) => {
        expect(token.IsDeprecated).toBe(true);
      });
    });

    it("generates correct navigation metadata for type name", () => {
      const mockTypeAlias = createMockTypeAlias(
        "NavigationType",
        "string",
        createBasicExcerptTokens("NavigationType", "string"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const nameToken = tokens.find((t) => t.Value === "NavigationType");
      expect(nameToken).toBeDefined();
      expect(nameToken?.NavigateToId).toBe("@test!NavigationType:type");
      expect(nameToken?.NavigationDisplayName).toBe("NavigationType");
      expect(nameToken?.RenderClasses).toEqual(["type"]);
    });

    it("handles complex nested generic types", () => {
      const typeParams = [createMockTypeParameter("T"), createMockTypeParameter("U", " keyof T")];
      const mockTypeAlias = createMockTypeAlias(
        "NestedGenericType",
        "Promise<Map<U, T[U]>>",
        createBasicExcerptTokens("NestedGenericType", "Promise<Map<U, T[U]>>"),
        typeParams,
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type NestedGenericType<T, U extends keyof T> = Promise<Map<U, T[U]>>;
      expect(tokens.length).toBeGreaterThan(0);

      // Check type parameters
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      expect(lessThanIndex).toBeGreaterThan(-1);

      // Check for extends keyword
      const extendsKeyword = tokens.find(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );
      expect(extendsKeyword).toBeDefined();
    });

    it("handles default export with deprecated type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "DeprecatedDefaultType",
        "string",
        createDefaultExportExcerptTokens("DeprecatedDefaultType", "string"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, true);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "default",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "type",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[3]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DeprecatedDefaultType",
        IsDeprecated: true,
      });
    });

    it("handles type parameter with constraint and default", () => {
      const typeParam = createMockTypeParameter("T", " object", "Record<string, unknown>");
      const mockTypeAlias = createMockTypeAlias(
        "ComplexParamType",
        "T & { timestamp: Date }",
        createBasicExcerptTokens("ComplexParamType", "T & { timestamp: Date }"),
        [typeParam],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type ComplexParamType<T extends object = Record<string, unknown>> = T & { timestamp: Date };
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      const paramSection = tokens.slice(lessThanIndex + 1, greaterThanIndex);

      // Should have extends keyword
      const extendsKeyword = paramSection.find(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );
      expect(extendsKeyword).toBeDefined();

      // Should have equals for default
      const equalsSign = paramSection.find((t) => t.Value === "=");
      expect(equalsSign).toBeDefined();
    });

    it("handles type alias referencing other types", () => {
      const mockTypeAlias = createMockTypeAlias(
        "CompositeType",
        "Partial<OtherType> & Required<AnotherType>",
        createBasicExcerptTokens("CompositeType", "Partial<OtherType> & Required<AnotherType>"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type CompositeType = Partial<OtherType> & Required<AnotherType>;
      expect(tokens.length).toBeGreaterThan(0);

      // Check for intersection
      const ampToken = tokens.find((t) => t.Value === "&");
      expect(ampToken).toBeDefined();
    });

    it("handles conditional type alias with union extends and infer", () => {
      const mockTypeAlias = createMockTypeAlias(
        "PaginateReturn",
        "TResult extends { body: { value?: infer TPage; }; } | { body: { members?: infer TPage; }; } ? GetArrayType<TPage> : Array<unknown>",
        createBasicExcerptTokens(
          "PaginateReturn",
          "TResult extends { body: { value?: infer TPage; }; } | { body: { members?: infer TPage; }; } ? GetArrayType<TPage> : Array<unknown>",
        ),
        [createMockTypeParameter("TResult")],
      );

      const { tokens, children } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const extendsToken = tokens.find(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );
      expect(extendsToken).toBeDefined();

      const questionToken = tokens.find((t) => t.Value === "?");
      expect(questionToken).toBeDefined();

      const colonToken = tokens.find((t) => t.Value === ":");
      expect(colonToken).toBeDefined();

      const inferToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "infer");
      expect(inferToken).toBeDefined();

      const memberToken = tokens.find(
        (t) => t.Kind === TokenKind.MemberName && t.Value === "members",
      );
      expect(memberToken).toBeDefined();

      const pipeTokenCount = tokens.filter(
        (t) => t.Kind === TokenKind.Punctuation && t.Value === "|",
      ).length;
      expect(pipeTokenCount).toBeGreaterThan(0);

      expect(children).toBeUndefined();
    });

    it("normalizes multiline conditional extends operands into one token line", () => {
      const conditionalType = `TResult extends {
  body: {
    value?: infer TPage;
  };
} | {
  body: {
    members?: infer TPage;
  };
} ? GetArrayType<TPage> : Array<unknown>`;

      const mockTypeAlias = createMockTypeAlias(
        "PaginateReturnMultiline",
        conditionalType,
        createBasicExcerptTokens("PaginateReturnMultiline", conditionalType),
        [createMockTypeParameter("TResult")],
      );

      const { tokens, children } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const inferToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "infer");
      expect(inferToken).toBeDefined();

      const valueToken = tokens.find((t) => t.Kind === TokenKind.MemberName && t.Value === "value");
      expect(valueToken).toBeDefined();

      const hasNewlineToken = tokens.some((t) => t.Value.includes("\n") || t.Value.includes("\r"));
      expect(hasNewlineToken).toBe(false);
      expect(children).toBeUndefined();
    });

    it("handles function type alias returning Promise of type literal", () => {
      const typeText = `(pageLink: string) => Promise<{
  page: TPage;
  nextPageLink?: string;
}>`;
      const mockTypeAlias = createMockTypeAlias(
        "GetPage",
        typeText,
        createBasicExcerptTokens("GetPage", typeText),
        [createMockTypeParameter("TPage")],
      );

      const { tokens, children } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const arrowToken = tokens.find((t) => t.Value === "=>");
      expect(arrowToken).toBeDefined();

      const promiseToken = tokens.find(
        (t) => t.Kind === TokenKind.TypeName && t.Value === "Promise",
      );
      expect(promiseToken).toBeDefined();

      const stringToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "string");
      expect(stringToken).toBeDefined();

      const nextPageLinkToken = tokens.find(
        (t) => t.Kind === TokenKind.MemberName && t.Value === "nextPageLink",
      );
      expect(nextPageLinkToken).toBeDefined();

      const pageToken = tokens.find((t) => t.Kind === TokenKind.MemberName && t.Value === "page");
      expect(pageToken).toBeDefined();

      expect(children).toBeUndefined();
    });

    it("classifies LHS identifiers and keyword-like keys as member names in inline object types", () => {
      const typeText = [
        "TResult extends {",
        "  body: {",
        "    default?: string;",
        "    members?: infer TPage;",
        "  };",
        "} ? TResult : never",
      ].join("\n");

      const mockTypeAlias = createMockTypeAlias(
        "InlineMemberNameKeys",
        typeText,
        createBasicExcerptTokens("InlineMemberNameKeys", typeText),
        [createMockTypeParameter("TResult")],
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const defaultMemberToken = tokens.find(
        (t) => t.Kind === TokenKind.MemberName && t.Value === "default",
      );
      expect(defaultMemberToken).toBeDefined();

      const defaultKeywordToken = tokens.find(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "default",
      );
      expect(defaultKeywordToken).toBeUndefined();

      const membersToken = tokens.find(
        (t) => t.Kind === TokenKind.MemberName && t.Value === "members",
      );
      expect(membersToken).toBeDefined();
    });

    it("detects readonly and template-literal related tokens in complex inline types", () => {
      const typeText = [
        "TResult extends {",
        "  body: {",
        "    readonly value?: infer TPage;",
        "    kind?: `\${string}-\${number}`;",
        "  };",
        "} ? GetArrayType<TPage> : Array<unknown>",
      ].join("\n");

      const mockTypeAlias = createMockTypeAlias(
        "InlineComplexTokens",
        typeText,
        createBasicExcerptTokens("InlineComplexTokens", typeText),
        [createMockTypeParameter("TResult")],
      );

      const { tokens, children } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      const readonlyToken = tokens.find(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "readonly",
      );
      expect(readonlyToken).toBeDefined();

      const inferToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "infer");
      expect(inferToken).toBeDefined();

      const stringToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "string");
      expect(stringToken).toBeDefined();

      const numberToken = tokens.find((t) => t.Kind === TokenKind.Keyword && t.Value === "number");
      expect(numberToken).toBeDefined();

      const kindToken = tokens.find((t) => t.Kind === TokenKind.MemberName && t.Value === "kind");
      expect(kindToken).toBeDefined();

      expect(children).toBeUndefined();
    });

    it("handles template literal type alias", () => {
      const mockTypeAlias = createMockTypeAlias(
        "TemplateLiteralType",
        "`${string}-${number}`",
        createBasicExcerptTokens("TemplateLiteralType", "`${string}-${number}`"),
      );

      const { tokens } = typeAliasTokenGenerator.generate(mockTypeAlias, false);

      // export type TemplateLiteralType = `${string}-${number}`;
      expect(tokens.length).toBeGreaterThan(0);
    });
  });

  describe("generate - Type Reference Navigation", () => {
    it("should generate navigateToId for type alias with type reference", () => {
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "MyAlias",
        excerptTokens: createBasicExcerptTokens("MyAlias", "Foo"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!MyAlias:type",
        },
        typeExcerpt: {
          text: "Foo",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "Foo",
              canonicalReference: {
                toString: () => "@azure/test!Foo:interface",
              },
            },
          ],
        },
      };

      const { tokens } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the Foo type token (not the type alias name)
      const fooTokens = tokens.filter((t) => t.Value === "Foo");
      // Should have at least one Foo token that's the type reference (not the alias name)
      const fooTypeToken = fooTokens.find((t) => t.NavigateToId === "@azure/test!Foo:interface");
      expect(fooTypeToken).toBeDefined();
      expect(fooTypeToken?.Kind).toBe(TokenKind.TypeName);
    });

    it("should generate navigateToId for type alias with union of type references", () => {
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "UnionAlias",
        excerptTokens: createBasicExcerptTokens("UnionAlias", "Foo | Bar"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!UnionAlias:type",
        },
        typeExcerpt: {
          text: "Foo | Bar",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "Foo",
              canonicalReference: {
                toString: () => "@azure/test!Foo:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: " | ",
            },
            {
              kind: ExcerptTokenKind.Reference,
              text: "Bar",
              canonicalReference: {
                toString: () => "@azure/test!Bar:interface",
              },
            },
          ],
        },
      };

      const { tokens } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the Foo type token
      const fooToken = tokens.find(
        (t) => t.Value === "Foo" && t.NavigateToId === "@azure/test!Foo:interface",
      );
      expect(fooToken).toBeDefined();
      expect(fooToken?.Kind).toBe(TokenKind.TypeName);

      // Find the Bar type token
      const barToken = tokens.find(
        (t) => t.Value === "Bar" && t.NavigateToId === "@azure/test!Bar:interface",
      );
      expect(barToken).toBeDefined();
      expect(barToken?.Kind).toBe(TokenKind.TypeName);
    });

    it("should generate navigateToId for type alias with generic type reference", () => {
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "PromiseAlias",
        excerptTokens: createBasicExcerptTokens("PromiseAlias", "Promise<User>"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!PromiseAlias:type",
        },
        typeExcerpt: {
          text: "Promise<User>",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "Promise",
              canonicalReference: {
                toString: () => "!Promise:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: "<",
            },
            {
              kind: ExcerptTokenKind.Reference,
              text: "User",
              canonicalReference: {
                toString: () => "@azure/test!User:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: ">",
            },
          ],
        },
      };

      const { tokens } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the Promise type token
      const promiseToken = tokens.find(
        (t) => t.Value === "Promise" && t.NavigateToId === "!Promise:interface",
      );
      expect(promiseToken).toBeDefined();
      expect(promiseToken?.Kind).toBe(TokenKind.TypeName);

      // Find the User type token
      const userToken = tokens.find(
        (t) => t.Value === "User" && t.NavigateToId === "@azure/test!User:interface",
      );
      expect(userToken).toBeDefined();
      expect(userToken?.Kind).toBe(TokenKind.TypeName);
    });

    it("should not generate navigateToId for primitive types", () => {
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "StringAlias",
        excerptTokens: createBasicExcerptTokens("StringAlias", "string"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!StringAlias:type",
        },
        typeExcerpt: {
          text: "string",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Content,
              text: "string",
            },
          ],
        },
      };

      const { tokens } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the string type token (after the = sign)
      const stringToken = tokens.find((t) => t.Value === "string");
      expect(stringToken).toBeDefined();
      // Primitive types should not have NavigateToId
      expect(stringToken?.NavigateToId).toBeUndefined();
    });

    it("should still generate children for inline type literals", () => {
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "ObjectAlias",
        excerptTokens: createBasicExcerptTokens("ObjectAlias", "{ name: string; }"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!ObjectAlias:type",
        },
        typeExcerpt: {
          text: "{ name: string; }",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Content,
              text: "{ name: string; }",
            },
          ],
        },
      };

      const { tokens, children } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Should have children for the inline object type
      expect(children).toBeDefined();
      expect(children!.length).toBeGreaterThan(0);
    });

    it("should generate navigateToId for type reference combined with inline type literal", () => {
      // This is the edge case where a type has BOTH inline literals AND type references
      // e.g., `Foo | { bar: string }`
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "CombinedAlias",
        excerptTokens: createBasicExcerptTokens("CombinedAlias", "Foo | { bar: string }"),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!CombinedAlias:type",
        },
        typeExcerpt: {
          text: "Foo | { bar: string }",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "Foo",
              canonicalReference: {
                toString: () => "@azure/test!Foo:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: " | { bar: string }",
            },
          ],
        },
      };

      const { tokens, children } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the Foo type token - should have navigation
      const fooToken = tokens.find(
        (t) => t.Value === "Foo" && t.NavigateToId === "@azure/test!Foo:interface",
      );
      expect(fooToken).toBeDefined();
      expect(fooToken?.Kind).toBe(TokenKind.TypeName);

      // Should have children for the inline type literal
      expect(children).toBeDefined();
      expect(children!.length).toBeGreaterThan(0);

      // Find the bar member in children
      const flattenTokens = (lines: typeof children): ReviewToken[] => {
        const result: ReviewToken[] = [];
        for (const line of lines ?? []) {
          if (line.Tokens) result.push(...line.Tokens);
          if (line.Children) result.push(...flattenTokens(line.Children));
        }
        return result;
      };
      const childTokens = flattenTokens(children);
      const barToken = childTokens.find((t) => t.Value === "bar");
      expect(barToken).toBeDefined();
      expect(barToken?.Kind).toBe(TokenKind.MemberName);
    });

    it("should generate navigateToId for multiple type references combined with inline type literal", () => {
      // More complex case: `Foo | Bar | { baz: number }`
      const mock: any = {
        kind: ApiItemKind.TypeAlias,
        displayName: "MultiCombinedAlias",
        excerptTokens: createBasicExcerptTokens(
          "MultiCombinedAlias",
          "Foo | Bar | { baz: number }",
        ),
        typeParameters: [],
        canonicalReference: {
          toString: () => "@test!MultiCombinedAlias:type",
        },
        typeExcerpt: {
          text: "Foo | Bar | { baz: number }",
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "Foo",
              canonicalReference: {
                toString: () => "@azure/test!Foo:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: " | ",
            },
            {
              kind: ExcerptTokenKind.Reference,
              text: "Bar",
              canonicalReference: {
                toString: () => "@azure/test!Bar:interface",
              },
            },
            {
              kind: ExcerptTokenKind.Content,
              text: " | { baz: number }",
            },
          ],
        },
      };

      const { tokens, children } = typeAliasTokenGenerator.generate(mock as ApiTypeAlias, false);

      // Find the Foo type token - should have navigation
      const fooToken = tokens.find(
        (t) => t.Value === "Foo" && t.NavigateToId === "@azure/test!Foo:interface",
      );
      expect(fooToken).toBeDefined();
      expect(fooToken?.Kind).toBe(TokenKind.TypeName);

      // Find the Bar type token - should have navigation
      const barToken = tokens.find(
        (t) => t.Value === "Bar" && t.NavigateToId === "@azure/test!Bar:interface",
      );
      expect(barToken).toBeDefined();
      expect(barToken?.Kind).toBe(TokenKind.TypeName);

      // Should have children for the inline type literal
      expect(children).toBeDefined();
      expect(children!.length).toBeGreaterThan(0);
    });
  });
});
