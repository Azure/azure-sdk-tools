import { describe, expect, it } from "vitest";
import { namespaceTokenGenerator } from "../../src/tokenGenerators/namespace";
import {
  ApiNamespace,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiNamespace
function createMockNamespace(
  displayName: string,
  excerptTokens: ExcerptToken[],
): ApiNamespace {
  const mock: any = {
    kind: ApiItemKind.Namespace,
    displayName,
    excerptTokens,
    canonicalReference: {
      toString: () => `@test!${displayName}:namespace`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    fileUrlPath: undefined,
    excerpt: {
      text: excerptTokens.map((t) => t.text).join(""),
      tokenRange: { startIndex: 0, endIndex: excerptTokens.length },
      tokens: excerptTokens,
    },
    releaseTag: undefined,
    tsdocComment: undefined,
    isExported: true,
  };
  return mock as ApiNamespace;
}

function createBasicExcerptTokens(name: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: `declare namespace ${name} ` },
  ] as ExcerptToken[];
}

describe("namespaceTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for namespace items", () => {
      const mock = { kind: ApiItemKind.Namespace, displayName: "KnownValues" } as ApiNamespace;
      expect(namespaceTokenGenerator.isValid(mock)).toBe(true);
    });

    it("returns false for non-namespace items", () => {
      const mockEnum = { kind: ApiItemKind.Enum, displayName: "TestEnum" } as ApiItem;
      expect(namespaceTokenGenerator.isValid(mockEnum)).toBe(false);
    });

    it("returns false for interface items", () => {
      const mock = { kind: ApiItemKind.Interface, displayName: "Foo" } as ApiItem;
      expect(namespaceTokenGenerator.isValid(mock)).toBe(false);
    });

    it("returns false for class items", () => {
      const mock = { kind: ApiItemKind.Class, displayName: "Foo" } as ApiItem;
      expect(namespaceTokenGenerator.isValid(mock)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a simple namespace", () => {
      const mock = createMockNamespace("KnownValues", createBasicExcerptTokens("KnownValues"));
      const { tokens, children } = namespaceTokenGenerator.generate(mock, false);

      expect(tokens).toHaveLength(3);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "declare",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "namespace",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "KnownValues",
        IsDeprecated: false,
        NavigateToId: "@test!KnownValues:namespace",
        NavigationDisplayName: "KnownValues",
        RenderClasses: ["namespace"],
      });
      expect(children).toBeUndefined();
    });

    it("generates correct tokens for a deprecated namespace", () => {
      const mock = createMockNamespace("OldNamespace", createBasicExcerptTokens("OldNamespace"));
      const { tokens } = namespaceTokenGenerator.generate(mock, true);

      expect(tokens).toHaveLength(3);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "declare",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "namespace",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: true,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.TypeName,
        Value: "OldNamespace",
        IsDeprecated: true,
        NavigateToId: "@test!OldNamespace:namespace",
        NavigationDisplayName: "OldNamespace",
        RenderClasses: ["namespace"],
      });
    });

    it("throws error for non-namespace items", () => {
      const mock: any = {
        kind: ApiItemKind.Interface,
        displayName: "NotANamespace",
        canonicalReference: { toString: () => "" },
      };
      expect(() => namespaceTokenGenerator.generate(mock as ApiNamespace)).toThrow(
        "Invalid item NotANamespace of kind Interface for Namespace token generator.",
      );
    });

    it("does not return children (container children handled by buildMember)", () => {
      const mock = createMockNamespace("MyNamespace", createBasicExcerptTokens("MyNamespace"));
      const result = namespaceTokenGenerator.generate(mock, false);
      expect(result.children).toBeUndefined();
    });

    it("sets NavigateToId from canonicalReference", () => {
      const mock = createMockNamespace("KnownFoo", createBasicExcerptTokens("KnownFoo"));
      const { tokens } = namespaceTokenGenerator.generate(mock, false);
      const nameToken = tokens[2];
      expect(nameToken.NavigateToId).toBe("@test!KnownFoo:namespace");
      expect(nameToken.NavigationDisplayName).toBe("KnownFoo");
    });
  });
});
