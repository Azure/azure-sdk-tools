import { describe, expect, it } from "vitest";
import { generateApiview } from "../src/generate";
import { ApiModel } from "@microsoft/api-extractor-model";
import path from "path";

describe("generate", () => {
  describe("enums", () => {
    it("generates correct models for renamed exports", () => {
      const model: ApiModel = new ApiModel();
      model.loadPackage(path.join(__dirname, "./data/renamedEnum.json"));

      const result = generateApiview({
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
});
