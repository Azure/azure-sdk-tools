import { describe, expect, it } from "vitest";
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
