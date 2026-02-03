import { ApiItemKind, ApiProperty, ApiPropertySignature } from "@microsoft/api-extractor-model";
import { expect } from "chai";
import { propertyTokenGenerator } from "../../src/tokenGenerators/property";
import { TokenKind } from "../../src/models";
import { describe, it } from "vitest";

describe("Property Token Generator", () => {
  describe("isValid", () => {
    it("should return true for ApiProperty", () => {
      const mockItem = { kind: ApiItemKind.Property } as ApiProperty;
      expect(propertyTokenGenerator.isValid(mockItem)).to.be.true;
    });

    it("should return true for ApiPropertySignature", () => {
      const mockItem = { kind: ApiItemKind.PropertySignature } as ApiPropertySignature;
      expect(propertyTokenGenerator.isValid(mockItem)).to.be.true;
    });

    it("should return false for other kinds", () => {
      const mockItem = { kind: ApiItemKind.Method } as any;
      expect(propertyTokenGenerator.isValid(mockItem)).to.be.false;
    });
  });

  describe("generate", () => {
    it("should generate tokens for a simple property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "myProperty",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens).to.have.lengthOf(4);
      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "myProperty" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Keyword, Value: "string" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ";" });
    });

    it("should generate tokens for a readonly property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "readonlyProp",
        isReadonly: true,
        isOptional: false,
        propertyTypeExcerpt: { text: "number" },
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlyProp" });
    });

    it("should generate tokens for an optional property", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "optionalProp",
        isReadonly: false,
        isOptional: true,
        propertyTypeExcerpt: { text: "boolean" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "optionalProp" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
    });

    it("should generate tokens for a static property", () => {
      const mockItem = Object.create(ApiProperty.prototype);
      Object.defineProperties(mockItem, {
        kind: { value: ApiItemKind.Property, writable: false },
        displayName: { value: "staticProp", writable: false },
        isStatic: { value: true, writable: false },
        isReadonly: { value: false, writable: false },
        isOptional: { value: false, writable: false },
        propertyTypeExcerpt: { value: { text: "string" }, writable: false },
      });

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "static" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "staticProp" });
    });

    it("should generate tokens for a static readonly property", () => {
      const mockItem = Object.create(ApiProperty.prototype);
      Object.defineProperties(mockItem, {
        kind: { value: ApiItemKind.Property, writable: false },
        displayName: { value: "staticReadonlyProp", writable: false },
        isStatic: { value: true, writable: false },
        isReadonly: { value: true, writable: false },
        isOptional: { value: false, writable: false },
        propertyTypeExcerpt: { value: { text: "string" }, writable: false },
      });

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "static" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
      expect(tokens[2]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "staticReadonlyProp",
      });
    });

    it("should mark tokens as deprecated when deprecated flag is set", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deprecatedProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedProp",
        IsDeprecated: true,
      });
    });

    it("should throw error for invalid item kind", () => {
      const mockItem = {
        kind: ApiItemKind.Method,
        displayName: "invalidItem",
      } as any;

      expect(() => propertyTokenGenerator.generate(mockItem)).to.throw();
    });
  });

  describe("generate - PropertySignature", () => {
    it("should generate tokens for a simple property signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "signatureProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens).to.have.lengthOf(4);
      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "signatureProp" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Keyword, Value: "string" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ";" });
    });

    it("should generate tokens for a readonly property signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "readonlySignature",
        isReadonly: true,
        isOptional: false,
        propertyTypeExcerpt: { text: "number" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlySignature" });
    });

    it("should generate tokens for an optional property signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "optionalSignature",
        isReadonly: false,
        isOptional: true,
        propertyTypeExcerpt: { text: "boolean" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "optionalSignature" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.Keyword, Value: "boolean" });
    });

    it("should generate tokens for a readonly optional property signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "readonlyOptional",
        isReadonly: true,
        isOptional: true,
        propertyTypeExcerpt: { text: "string[]" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlyOptional" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
      // string[] is parsed as: string (Keyword) + [ + ]
      expect(tokens[4]).to.deep.include({ Kind: TokenKind.Keyword, Value: "string" });
      expect(tokens[5]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "[" });
      expect(tokens[6]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "]" });
    });

    it("should not add static keyword for property signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "interfaceProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      const hasStatic = tokens.some((t) => t.Value === "static");
      expect(hasStatic).to.be.false;
    });

    it("should mark property signature tokens as deprecated", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "deprecatedSignature",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedSignature",
        IsDeprecated: true,
      });
      expect(tokens[2]).to.deep.include({
        Kind: TokenKind.Keyword,
        Value: "string",
        IsDeprecated: true,
      });
    });
  });

  describe("generate - Getter", () => {
    it("should generate tokens for a getter property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "myGetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
        excerptTokens: [{ text: "get myGetter(): " }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "get" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "myGetter" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "(" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ")" });
      expect(tokens[4]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
    });

    it("should generate tokens for a getter with complex return type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "complexGetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "Promise<string>" },
        excerptTokens: [{ text: "get complexGetter(): " }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "get" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "complexGetter" });
    });

    it("should mark getter tokens as deprecated when deprecated flag is set", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deprecatedGetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
        excerptTokens: [{ text: "get deprecatedGetter(): " }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.Keyword,
        Value: "get",
        IsDeprecated: true,
      });
      expect(tokens[1]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedGetter",
        IsDeprecated: true,
      });
    });

    it("should handle getter with leading whitespace in excerpt", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "spacedGetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "number" },
        excerptTokens: [{ text: "  get spacedGetter(): " }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "get" });
    });

    it("should generate children for getter with inline object type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "objectGetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ name: string; }" },
        excerptTokens: [{ text: "get objectGetter(): " }],
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "get" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "objectGetter" });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });
  });

  describe("generate - Setter", () => {
    it("should generate tokens for a setter property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "mySetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
        excerptTokens: [{ text: "set mySetter(value: string)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "mySetter" });
      expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "(" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.MemberName, Value: "value" });
      expect(tokens[4]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
    });

    it("should generate tokens for a setter with custom parameter name", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "customSetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "number" },
        excerptTokens: [{ text: "set customSetter(newValue: number)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "customSetter" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.MemberName, Value: "newValue" });
    });

    it("should generate tokens for a setter with complex parameter type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "complexSetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "Promise<string>" },
        excerptTokens: [{ text: "set complexSetter(value: Promise<string>)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "complexSetter" });
    });

    it("should mark setter tokens as deprecated when deprecated flag is set", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deprecatedSetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
        excerptTokens: [{ text: "set deprecatedSetter(value: string)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.Keyword,
        Value: "set",
        IsDeprecated: true,
      });
      expect(tokens[1]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedSetter",
        IsDeprecated: true,
      });
    });

    it("should handle setter with leading whitespace in excerpt", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "spacedSetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "boolean" },
        excerptTokens: [{ text: "   set spacedSetter(val: boolean)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
    });

    it("should generate tokens for a setter with array type parameter", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "arraySetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string[]" },
        excerptTokens: [{ text: "set arraySetter(items: string[])" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "arraySetter" });
      expect(tokens[3]).to.deep.include({ Kind: TokenKind.MemberName, Value: "items" });
    });

    it("should generate tokens for a setter with union type parameter", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "unionSetter",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string | number" },
        excerptTokens: [{ text: "set unionSetter(value: string | number)" }],
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "unionSetter" });
    });
  });
});
