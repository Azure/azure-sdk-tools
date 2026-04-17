import { describe, expect, it } from "vitest";
import { interfaceTokenGenerator } from "../../src/tokenGenerators/interfaces";
import {
  ApiInterface,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
  TypeParameter,
  HeritageType,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiInterface with all required properties
function createMockInterface(
  displayName: string,
  excerptTokens: ExcerptToken[],
  typeParameters?: TypeParameter[],
  extendsTypes?: HeritageType[],
): ApiInterface {
  const mock: any = {
    kind: ApiItemKind.Interface,
    displayName,
    excerptTokens,
    typeParameters: typeParameters || [],
    extendsTypes: extendsTypes || [],
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!${displayName}:interface`,
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
    // Additional required properties
    releaseTag: undefined,
    tsdocComment: undefined,
  };
  return mock as ApiInterface;
}

// Helper to create basic excerpt tokens
function createBasicExcerptTokens(interfaceName: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export " },
    { kind: ExcerptTokenKind.Content, text: "interface " },
    { kind: ExcerptTokenKind.Content, text: interfaceName },
  ] as ExcerptToken[];
}

// Helper to create excerpt tokens with default export
function createDefaultExportExcerptTokens(interfaceName: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export default " },
    { kind: ExcerptTokenKind.Content, text: "interface " },
    { kind: ExcerptTokenKind.Content, text: interfaceName },
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

// Helper to create a mock HeritageType
function createMockHeritageType(typeName: string): HeritageType {
  return {
    excerpt: {
      text: typeName,
      spannedTokens: [{ kind: ExcerptTokenKind.Reference, text: typeName }],
    },
  } as unknown as HeritageType;
}

describe("interfaceTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for interface items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiInterface;

      expect(interfaceTokenGenerator.isValid(mockInterface)).toBe(true);
    });

    it("returns false for non-interface items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as ApiItem;

      expect(interfaceTokenGenerator.isValid(mockEnum)).toBe(false);
    });

    it("returns false for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiItem;

      expect(interfaceTokenGenerator.isValid(mockClass)).toBe(false);
    });

    it("returns false for function items", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "testFunction",
      } as ApiItem;

      expect(interfaceTokenGenerator.isValid(mockFunction)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a simple non-deprecated interface", () => {
      const mockInterface = createMockInterface(
        "SimpleInterface",
        createBasicExcerptTokens("SimpleInterface"),
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      expect(tokens).toHaveLength(3);
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
        Value: "interface",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "SimpleInterface",
        IsDeprecated: false,
        NavigateToId: "@test!SimpleInterface:interface",
        NavigationDisplayName: "SimpleInterface",
        RenderClasses: ["interface"],
      });
    });

    it("generates correct tokens for a deprecated interface", () => {
      const mockInterface = createMockInterface(
        "DeprecatedInterface",
        createBasicExcerptTokens("DeprecatedInterface"),
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, true);

      expect(tokens).toHaveLength(3);
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
        Value: "interface",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DeprecatedInterface",
        IsDeprecated: true,
      });
    });

    it("generates correct tokens for a default exported interface", () => {
      const mockInterface = createMockInterface(
        "DefaultInterface",
        createDefaultExportExcerptTokens("DefaultInterface"),
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      expect(tokens).toHaveLength(4);
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
        Value: "interface",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[3]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DefaultInterface",
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for an interface with a single type parameter", () => {
      const typeParam = createMockTypeParameter("T");
      const mockInterface = createMockInterface(
        "GenericInterface",
        createBasicExcerptTokens("GenericInterface"),
        [typeParam],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface GenericInterface<T>
      expect(tokens.length).toBeGreaterThan(3);

      // Find the type parameter tokens
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

    it("generates correct tokens for an interface with multiple type parameters", () => {
      const typeParams = [
        createMockTypeParameter("T"),
        createMockTypeParameter("U"),
        createMockTypeParameter("V"),
      ];
      const mockInterface = createMockInterface(
        "MultiGenericInterface",
        createBasicExcerptTokens("MultiGenericInterface"),
        typeParams,
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface MultiGenericInterface<T, U, V>
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      expect(lessThanIndex).toBeGreaterThan(-1);
      expect(greaterThanIndex).toBeGreaterThan(lessThanIndex);

      // Check that we have three type parameters separated by commas
      const paramSection = tokens.slice(lessThanIndex + 1, greaterThanIndex);
      const typeParamNames = paramSection
        .filter((t) => t.Kind === TokenKind.TypeName)
        .map((t) => t.Value);

      expect(typeParamNames).toEqual(["T", "U", "V"]);

      // Check for commas
      const commas = paramSection.filter((t) => t.Value === ",");
      expect(commas).toHaveLength(2);
    });

    it("generates correct tokens for an interface with a constrained type parameter", () => {
      const typeParam = createMockTypeParameter("T", " string");
      const mockInterface = createMockInterface(
        "ConstrainedInterface",
        createBasicExcerptTokens("ConstrainedInterface"),
        [typeParam],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface ConstrainedInterface<T extends string>
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

    it("generates correct tokens for an interface extending a single interface", () => {
      const extendsType = createMockHeritageType("BaseInterface");
      const mockInterface = createMockInterface(
        "ExtendedInterface",
        createBasicExcerptTokens("ExtendedInterface"),
        [],
        [extendsType],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface ExtendedInterface extends BaseInterface
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

      // Check for the base interface reference
      const referenceIndex = tokens.findIndex((t) => t.Value === "BaseInterface");
      expect(referenceIndex).toBeGreaterThan(extendsKeywordIndex);
    });

    it("generates correct tokens for an interface extending multiple interfaces", () => {
      const extendsTypes = [
        createMockHeritageType("BaseInterface1"),
        createMockHeritageType("BaseInterface2"),
        createMockHeritageType("BaseInterface3"),
      ];
      const mockInterface = createMockInterface(
        "MultiExtendedInterface",
        createBasicExcerptTokens("MultiExtendedInterface"),
        [],
        extendsTypes,
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface MultiExtendedInterface extends BaseInterface1, BaseInterface2, BaseInterface3
      const extendsKeywordIndex = tokens.findIndex(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );

      expect(extendsKeywordIndex).toBeGreaterThan(-1);

      // Check for all base interface references after extends keyword
      const afterExtends = tokens.slice(extendsKeywordIndex + 1);
      const baseInterfaceReferences = afterExtends.filter((t) =>
        t.Value?.startsWith("BaseInterface"),
      );

      expect(baseInterfaceReferences.length).toBe(3);

      // Check for commas
      const commas = afterExtends.filter((t) => t.Value === ",");
      expect(commas).toHaveLength(2);
    });

    it("generates correct tokens for an interface with both type parameters and extends clause", () => {
      const typeParam = createMockTypeParameter("T");
      const extendsType = createMockHeritageType("BaseInterface");
      const mockInterface = createMockInterface(
        "ComplexInterface",
        createBasicExcerptTokens("ComplexInterface"),
        [typeParam],
        [extendsType],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface ComplexInterface<T> extends BaseInterface
      // Should have both type parameters and extends clause
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");
      const extendsKeywordIndex = tokens.findIndex(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );

      expect(lessThanIndex).toBeGreaterThan(-1);
      expect(greaterThanIndex).toBeGreaterThan(lessThanIndex);
      expect(extendsKeywordIndex).toBeGreaterThan(greaterThanIndex);
    });

    it("throws an error for invalid item kind", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
        excerptTokens: [],
      } as any;

      expect(() => {
        interfaceTokenGenerator.generate(mockClass, false);
      }).toThrow("Invalid item TestClass of kind Class for Interface token generator.");
    });

    it("preserves deprecated state across all tokens", () => {
      const typeParam = createMockTypeParameter("T");
      const extendsType = createMockHeritageType("BaseInterface");
      const mockInterface = createMockInterface(
        "DeprecatedComplexInterface",
        createBasicExcerptTokens("DeprecatedComplexInterface"),
        [typeParam],
        [extendsType],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, true);

      // All tokens should have IsDeprecated: true
      tokens.forEach((token) => {
        expect(token.IsDeprecated).toBe(true);
      });
    });

    it("generates correct navigation metadata for interface name", () => {
      const mockInterface = createMockInterface(
        "NavigationInterface",
        createBasicExcerptTokens("NavigationInterface"),
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      const nameToken = tokens.find((t) => t.Value === "NavigationInterface");
      expect(nameToken).toBeDefined();
      expect(nameToken?.NavigateToId).toBe("@test!NavigationInterface:interface");
      expect(nameToken?.NavigationDisplayName).toBe("NavigationInterface");
      expect(nameToken?.RenderClasses).toEqual(["interface"]);
    });

    it("handles type parameter with complex constraint", () => {
      const typeParam = createMockTypeParameter("T", " Array<string> | Map<string, number>");
      const mockInterface = createMockInterface(
        "ComplexConstraintInterface",
        createBasicExcerptTokens("ComplexConstraintInterface"),
        [typeParam],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // Should have extends keyword
      const extendsKeywordIndex = tokens.findIndex(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );

      expect(extendsKeywordIndex).toBeGreaterThan(-1);

      // The constraint text should be processed
      const afterExtends = tokens.slice(extendsKeywordIndex + 1);
      expect(afterExtends.length).toBeGreaterThan(0);
    });

    it("handles empty type parameter constraint", () => {
      const typeParam = createMockTypeParameter("T", "");
      const mockInterface = createMockInterface(
        "UnconstrainedInterface",
        createBasicExcerptTokens("UnconstrainedInterface"),
        [typeParam],
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // Should NOT have extends keyword
      const extendsKeywords = tokens.filter(
        (t) => t.Kind === TokenKind.Keyword && t.Value === "extends",
      );

      expect(extendsKeywords).toHaveLength(0);
    });

    it("generates correct tokens for type parameters with default types", () => {
      const typeParams = [
        createMockTypeParameter("TElement"),
        createMockTypeParameter("TPage", undefined, "TElement[]"),
        createMockTypeParameter("TPageSettings", undefined, "PageSettings"),
      ];
      const mockInterface = createMockInterface(
        "PagedAsyncIterableIterator",
        createBasicExcerptTokens("PagedAsyncIterableIterator"),
        typeParams,
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, false);

      // export interface PagedAsyncIterableIterator<TElement, TPage = TElement[], TPageSettings = PageSettings>
      const lessThanIndex = tokens.findIndex((t) => t.Value === "<");
      const greaterThanIndex = tokens.findIndex((t) => t.Value === ">");

      expect(lessThanIndex).toBeGreaterThan(-1);
      expect(greaterThanIndex).toBeGreaterThan(lessThanIndex);

      // Check for default type assignments
      const paramSection = tokens.slice(lessThanIndex + 1, greaterThanIndex);
      const equalsSigns = paramSection.filter((t) => t.Value === "=");

      // Should have two equals signs (for TPage and TPageSettings defaults)
      expect(equalsSigns).toHaveLength(2);

      // Verify the structure: TPage = TElement[]
      const tPageIndex = paramSection.findIndex((t) => t.Value === "TPage");
      expect(tPageIndex).toBeGreaterThan(-1);

      const firstEqualsIndex = paramSection.findIndex((t) => t.Value === "=");
      expect(firstEqualsIndex).toBeGreaterThan(tPageIndex);

      // Check that default value follows the equals sign
      expect(paramSection[firstEqualsIndex + 1].Value).toBe("TElement[]");
    });

    it("handles default export with deprecated interface", () => {
      const mockInterface = createMockInterface(
        "DeprecatedDefaultInterface",
        createDefaultExportExcerptTokens("DeprecatedDefaultInterface"),
      );

      const { tokens } = interfaceTokenGenerator.generate(mockInterface, true);

      expect(tokens).toHaveLength(4);
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
        Value: "interface",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[3]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "DeprecatedDefaultInterface",
        IsDeprecated: true,
      });
    });
  });
});
