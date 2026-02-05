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

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;
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

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Verify specific tokens exist with correct values
      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedSignature",
      });
      expect(tokens[2]).to.deep.include({
        Kind: TokenKind.Keyword,
        Value: "string",
      });
    });

    it("should mark optional property signature tokens as deprecated including question mark", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "deprecatedOptionalSignature",
        isReadonly: false,
        isOptional: true,
        propertyTypeExcerpt: { text: "boolean" },
      } as unknown as ApiPropertySignature;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Verify specific tokens
      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "deprecatedOptionalSignature",
      });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
      expect(tokens[1].IsDeprecated).to.be.true;
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

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Verify specific tokens exist with correct values
      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "get" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "deprecatedGetter" });
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

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Verify specific tokens exist with correct values
      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "set" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "deprecatedSetter" });
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

  describe("generate - PropertySignature", () => {
    it("should mark all tokens including optional marker as deprecated", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deprecatedOptionalProp",
        isReadonly: false,
        isOptional: true,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiProperty;

      const { tokens } = propertyTokenGenerator.generate(mockItem, true);

      // All tokens should be marked as deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Verify the optional marker specifically
      const questionToken = tokens.find((t) => t.Value === "?");
      expect(questionToken).to.exist;
      expect(questionToken?.IsDeprecated).to.be.true;
    });
  });

  describe("generate - Nested Properties", () => {
    it("should generate children for property with inline object type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "nestedProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ name: string; age: number; }" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "nestedProp" });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for readonly property with inline object type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "readonlyNestedProp",
        isReadonly: true,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ id: string; }" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
      expect(tokens[1]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "readonlyNestedProp",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for optional property with inline object type", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "optionalNestedProp",
        isReadonly: false,
        isOptional: true,
        propertyTypeExcerpt: { text: "{ value: boolean; }" },
      } as unknown as ApiPropertySignature;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "optionalNestedProp",
      });
      expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should mark all children tokens as deprecated when property is deprecated", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deprecatedNestedProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ name: string; }" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem, true);

      // Parent tokens should be deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).to.be.true;

      // Children should exist and all their tokens should be deprecated
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
      children!.forEach((child) => {
        child.Tokens?.forEach((token) => {
          expect(token.IsDeprecated).to.be.true;
        });
      });
    });

    it("should generate children for property with nested object containing multiple properties", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "complexNestedProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ firstName: string; lastName: string; age: number; }" },
      } as unknown as ApiPropertySignature;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "complexNestedProp" });
      expect(children).to.exist;
      // Should have children for each property plus closing brace
      expect(children!.length).to.be.greaterThan(1);
    });

    it("should generate children for property with deeply nested object type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "deeplyNestedProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ outer: { inner: string; }; }" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "deeplyNestedProp" });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for property with array of objects type", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "arrayOfObjectsProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ name: string; }[]" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "arrayOfObjectsProp",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for property with union type containing inline object", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "unionWithObjectProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string | { custom: boolean; }" },
      } as unknown as ApiPropertySignature;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "unionWithObjectProp",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should not generate children for simple type property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "simpleProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "simpleProp" });
      expect(children).to.be.undefined;
    });

    it("should not generate children for array of primitives property", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "primitiveArrayProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "string[]" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "primitiveArrayProp",
      });
      expect(children).to.be.undefined;
    });

    it("should generate children with context end line for inline object", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "objectWithEndLine",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ prop: string; }" },
      } as unknown as ApiProperty;

      const { children } = propertyTokenGenerator.generate(mockItem);

      expect(children).to.exist;
      // Last child should be the context end line with closing brace
      const lastChild = children![children!.length - 1];
      expect(lastChild.IsContextEndLine).to.be.true;
    });

    it("should generate children for property with optional nested properties", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "nestedOptionalProps",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ required: string; optional?: number; }" },
      } as unknown as ApiPropertySignature;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "nestedOptionalProps",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for property with readonly nested properties", () => {
      const mockItem = {
        kind: ApiItemKind.Property,
        displayName: "nestedReadonlyProps",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ readonly id: string; readonly name: string; }" },
      } as unknown as ApiProperty;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "nestedReadonlyProps",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });

    it("should generate children for property with index signature", () => {
      const mockItem = {
        kind: ApiItemKind.PropertySignature,
        displayName: "indexSignatureProp",
        isReadonly: false,
        isOptional: false,
        propertyTypeExcerpt: { text: "{ [key: string]: number; }" },
      } as unknown as ApiPropertySignature;

      const { tokens, children } = propertyTokenGenerator.generate(mockItem);

      expect(tokens[0]).to.deep.include({
        Kind: TokenKind.MemberName,
        Value: "indexSignatureProp",
      });
      expect(children).to.exist;
      expect(children).to.have.length.greaterThan(0);
    });
  });
});
