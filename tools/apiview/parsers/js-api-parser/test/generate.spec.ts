import { describe, expect, it, beforeAll } from "vitest";
import { buildDependencies, generateApiView } from "../src/generate";
import { ApiModel } from "@microsoft/api-extractor-model";
import path from "path";
import { ReviewLine } from "../src/models";

describe("generate", () => {
  describe("enums", () => {
    it("generates correct models for renamed exports", () => {
      const model: ApiModel = new ApiModel();
      model.loadPackage(path.join(__dirname, "./data/renamedEnum.json"));

      const result = generateApiView({
        apiModel: model,
        dependencies: {},
        meta: {
          Name: "",
          PackageName: "",
          PackageVersion: "",
          ParserVersion: "",
          Language: "JavaScript",
        },
      });
      const enumReviewLine = result.ReviewLines[0].Children?.find(
        (c) => c.LineId === "@azure/test-package!KnownFoo:enum",
      );
      expect(enumReviewLine).toBeDefined();
      expect(enumReviewLine?.Tokens.map((t) => t.Value).join(" ")).toEqual(
        "export enum KnownFoo { }",
      );
    });

    describe("enum variations end-to-end", () => {
      let result: ReturnType<typeof generateApiView>;
      
      beforeAll(() => {
        const model: ApiModel = new ApiModel();
        model.loadPackage(path.join(__dirname, "./data/enumVariations.json"));

        result = generateApiView({
          apiModel: model,
          dependencies: {},
          meta: {
            Name: "",
            PackageName: "",
            PackageVersion: "",
            ParserVersion: "",
            Language: "JavaScript",
          },
        });
      });

      it("generates simple enum with members correctly", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!SimpleEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        
        // Check the declaration tokens (should be: export enum SimpleEnum {)
        const declarationTokens = enumLine?.Tokens.map((t) => t.Value).join(" ");
        expect(declarationTokens).toEqual("export enum SimpleEnum {");
        
        // Check that it has 3 children (one per member)
        expect(enumLine?.Children).toHaveLength(3);
        
        // Check that all expected members are present (order may vary)
        const memberIds = enumLine?.Children?.map((c) => c.LineId) ?? [];
        expect(memberIds).toContain("@azure/enum-test!SimpleEnum.ValueOne:member");
        expect(memberIds).toContain("@azure/enum-test!SimpleEnum.ValueTwo:member");
        expect(memberIds).toContain("@azure/enum-test!SimpleEnum.ValueThree:member");
        
        // Verify member tokens contain expected values
        const memberTokens = enumLine?.Children?.flatMap((c) => c.Tokens.map((t) => t.Value).join("")) ?? [];
        expect(memberTokens.some((t) => t.includes("ValueOne"))).toBe(true);
        expect(memberTokens.some((t) => t.includes("ValueTwo"))).toBe(true);
        expect(memberTokens.some((t) => t.includes("ValueThree"))).toBe(true);
      });

      it("generates numeric enum correctly", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!NumericEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        expect(enumLine?.Tokens.map((t) => t.Value).join(" ")).toEqual("export enum NumericEnum {");
        expect(enumLine?.Children).toHaveLength(2);
        
        // Check that both members are present (order may vary)
        const memberIds = enumLine?.Children?.map((c) => c.LineId) ?? [];
        expect(memberIds).toContain("@azure/enum-test!NumericEnum.Zero:member");
        expect(memberIds).toContain("@azure/enum-test!NumericEnum.One:member");
      });

      it("generates deprecated enum with deprecated flag on tokens", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!DeprecatedEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        
        // Declaration tokens (excluding the opening brace) should have IsDeprecated: true
        const declarationTokens = enumLine?.Tokens.filter((t) => t.Value !== "{") ?? [];
        const allTokensDeprecated = declarationTokens.every((t) => t.IsDeprecated === true);
        expect(allTokensDeprecated).toBe(true);
        
        // Verify the actual tokens are correct
        expect(declarationTokens[0].Value).toBe("export");
        expect(declarationTokens[1].Value).toBe("enum");
        expect(declarationTokens[2].Value).toBe("DeprecatedEnum");
        
        expect(enumLine?.Children).toHaveLength(1);
      });

      it("generates nested enum in namespace correctly", () => {
        const namespaceLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!MyNamespace:namespace",
        );
        
        expect(namespaceLine).toBeDefined();
        
        // Find the nested enum within the namespace children
        const nestedEnum = namespaceLine?.Children?.find(
          (c) => c.LineId === "@azure/enum-test!MyNamespace.NestedEnum:enum",
        );
        
        expect(nestedEnum).toBeDefined();
        expect(nestedEnum?.Tokens.map((t) => t.Value).join(" ")).toContain("enum NestedEnum");
        expect(nestedEnum?.Children).toHaveLength(2);
        expect(nestedEnum?.Children?.[0].LineId).toBe("@azure/enum-test!MyNamespace.NestedEnum.First:member");
        expect(nestedEnum?.Children?.[1].LineId).toBe("@azure/enum-test!MyNamespace.NestedEnum.Second:member");
      });

      it("generates deeply nested enum in namespace hierarchy", () => {
        const namespaceLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!MyNamespace:namespace",
        );
        
        // Find nested namespace
        const nestedNamespace = namespaceLine?.Children?.find(
          (c) => c.LineId === "@azure/enum-test!MyNamespace.Nested:namespace",
        );
        
        expect(nestedNamespace).toBeDefined();
        
        // Find deeply nested enum
        const deepEnum = nestedNamespace?.Children?.find(
          (c) => c.LineId === "@azure/enum-test!MyNamespace.Nested.DeeplyNestedEnum:enum",
        );
        
        expect(deepEnum).toBeDefined();
        expect(deepEnum?.Tokens.map((t) => t.Value).join(" ")).toContain("enum DeeplyNestedEnum");
        expect(deepEnum?.Children).toHaveLength(1);
        expect(deepEnum?.Children?.[0].LineId).toBe("@azure/enum-test!MyNamespace.Nested.DeeplyNestedEnum.DeepValue:member");
      });

      it("generates beta enum with @beta tag", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!BetaEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        
        // Find the @beta tag line (should be before the enum line in ReviewLines)
        const enumIndex = result.ReviewLines[0].Children?.findIndex(
          (c) => c.LineId === "@azure/enum-test!BetaEnum:enum",
        );
        
        if (enumIndex !== undefined && enumIndex > 0) {
          const previousLine = result.ReviewLines[0].Children?.[enumIndex - 1];
          const hasBetaTag = previousLine?.Tokens.some((t) => t.Value === "@beta");
          expect(hasBetaTag).toBe(true);
        }
      });

      it("ensures enum structure uses Children array not inline tokens", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!SimpleEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        
        // The enum line should have opening brace but not closing brace
        const hasOpeningBrace = enumLine?.Tokens.some((t) => t.Value === "{");
        const hasClosingBrace = enumLine?.Tokens.some((t) => t.Value === "}");
        
        expect(hasOpeningBrace).toBe(true);
        expect(hasClosingBrace).toBe(false); // Closing brace should be on separate line
        
        // Members should be in Children array, not as tokens on the enum line
        expect(enumLine?.Children).toBeDefined();
        expect(enumLine?.Children!.length).toBeGreaterThan(0);
      });

      it("verifies closing brace is on separate line with IsContextEndLine", () => {
        const enumLine = result.ReviewLines[0].Children?.find(
          (c) => c.LineId === "@azure/enum-test!SimpleEnum:enum",
        );
        
        expect(enumLine).toBeDefined();
        
        // Find the enum in the main ReviewLines array
        const enumIndex = result.ReviewLines[0].Children?.findIndex(
          (c) => c.LineId === "@azure/enum-test!SimpleEnum:enum",
        );
        
        if (enumIndex !== undefined && enumIndex >= 0) {
          // The next line should be the closing brace
          const nextLine = result.ReviewLines[0].Children?.[enumIndex + 1];
          
          expect(nextLine?.Tokens.some((t) => t.Value === "}")).toBe(true);
          expect(nextLine?.IsContextEndLine).toBe(true);
          expect(nextLine?.RelatedToLine).toBe("@azure/enum-test!SimpleEnum:enum");
        }
      });
    });
  });

  describe("dependencies are skipped properly", () => {
    it("skips azure dependencies", () => {
      const dependencies = {
        "@azure/package": "1.0.0",
        "@azure-rest/package": "1.0.0-beta.1",
      };
      const reviewLines: ReviewLine[] = [];
      buildDependencies(reviewLines, dependencies);
      expect(reviewLines[0].LineId).toEqual("Dependencies");
      const children = reviewLines[0].Children;

      expect(children?.length).toEqual(2);
      expect(children?.[0].Tokens?.[0].Value).toEqual("@azure/package");
      expect(children?.[0].Tokens?.[0].SkipDiff).toEqual(true);
      expect(children?.[0].Tokens?.[2].SkipDiff).toEqual(true);
      expect(children?.[1].Tokens?.[0].Value).toEqual("@azure-rest/package");
      expect(children?.[1].Tokens?.[0].SkipDiff).toEqual(true);
      expect(children?.[1].Tokens?.[2].SkipDiff).toEqual(true);
    });

    it("skips tslib", () => {
      const dependencies = {
        tslib: "1.0.0",
      };
      const reviewLines: ReviewLine[] = [];
      buildDependencies(reviewLines, dependencies);
      expect(reviewLines[0].LineId).toEqual("Dependencies");
      const children = reviewLines[0].Children;

      expect(children?.length).toEqual(1);
      expect(children?.[0].Tokens?.[0].Value).toEqual("tslib");
      expect(children?.[0].Tokens?.[0].SkipDiff).toEqual(true);
      expect(children?.[0].Tokens?.[2].SkipDiff).toEqual(true);
    });

    it("does not skip third-party dependencies", () => {
      const dependencies = {
        "fast-xml-parser": "5.0.7",
      };
      const reviewLines: ReviewLine[] = [];
      buildDependencies(reviewLines, dependencies);
      expect(reviewLines[0].LineId).toEqual("Dependencies");
      const children = reviewLines[0].Children;

      expect(children?.length).toEqual(1);
      expect(children?.[0].Tokens?.[0].Value).toEqual("fast-xml-parser");
      expect(children?.[0].Tokens?.[0].SkipDiff).toEqual(false);
      expect(children?.[0].Tokens?.[2].SkipDiff).toEqual(true);
    });
  });

  describe("cross-language IDs", () => {
    it("sets CrossLanguageId when mapping is provided", () => {
      const model: ApiModel = new ApiModel();
      model.loadPackage(path.join(__dirname, "./data/renamedEnum.json"));

      const crossLanguageDefinitionIds = {
        "@azure/test-package!KnownFoo:enum": "TestPackage.KnownFoo",
      };

      const result = generateApiView({
        apiModel: model,
        dependencies: {},
        meta: {
          Name: "",
          PackageName: "",
          PackageVersion: "",
          ParserVersion: "",
          Language: "JavaScript",
        },
        crossLanguageDefinitionIds,
      });

      const enumReviewLine = result.ReviewLines[0].Children?.find(
        (c: ReviewLine) => c.LineId === "@azure/test-package!KnownFoo:enum",
      );
      expect(enumReviewLine).toBeDefined();
      expect(enumReviewLine?.CrossLanguageId).toEqual("TestPackage.KnownFoo");
    });

    it("does not set CrossLanguageId when mapping is not provided", () => {
      const model: ApiModel = new ApiModel();
      model.loadPackage(path.join(__dirname, "./data/renamedEnum.json"));

      const result = generateApiView({
        apiModel: model,
        dependencies: {},
        meta: {
          Name: "",
          PackageName: "",
          PackageVersion: "",
          ParserVersion: "",
          Language: "JavaScript",
        },
      });

      const enumReviewLine = result.ReviewLines[0].Children?.find(
        (c: ReviewLine) => c.LineId === "@azure/test-package!KnownFoo:enum",
      );
      expect(enumReviewLine).toBeDefined();
      expect(enumReviewLine?.CrossLanguageId).toBeUndefined();
    });

    it("does not set CrossLanguageId when item is not in mapping", () => {
      const model: ApiModel = new ApiModel();
      model.loadPackage(path.join(__dirname, "./data/renamedEnum.json"));

      const crossLanguageDefinitionIds = {
        "@azure/test-package!SomeOtherItem:interface": "TestPackage.SomeOtherItem",
      };

      const result = generateApiView({
        apiModel: model,
        dependencies: {},
        meta: {
          Name: "",
          PackageName: "",
          PackageVersion: "",
          ParserVersion: "",
          Language: "JavaScript",
        },
        crossLanguageDefinitionIds,
      });

      const enumReviewLine = result.ReviewLines[0].Children?.find(
        (c: ReviewLine) => c.LineId === "@azure/test-package!KnownFoo:enum",
      );
      expect(enumReviewLine).toBeDefined();
      expect(enumReviewLine?.CrossLanguageId).toBeUndefined();
    });
  });
});
