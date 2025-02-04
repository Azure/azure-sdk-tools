import { describe, expect, it } from "vitest";
import { generateApiview } from "../src/generate";
import {
  ApiEntryPoint,
  ApiEnum,
  ApiModel,
  ApiPackage,
  ExcerptTokenKind,
  ReleaseTag,
} from "@microsoft/api-extractor-model";
import { DocComment, TSDocConfiguration } from "@microsoft/tsdoc";

describe("generate", () => {
  describe("enums", () => {
    it("generates correct models for renamed exports", () => {
      // TODO: work with Jeremy on better testing approaches for this
      const model: ApiModel = new ApiModel();
      const apiPackage = new ApiPackage({
        docComment: new DocComment({ configuration: new TSDocConfiguration() }),
        name: "test",
        tsdocConfiguration: new TSDocConfiguration(),
      });
      const entryPoint = new ApiEntryPoint({ name: "test" });
      const apiEnum = new ApiEnum({
        docComment: new DocComment({ configuration: new TSDocConfiguration() }),
        excerptTokens: [
          {
            kind: ExcerptTokenKind.Content,
            text: "export declare enum KnownJsonWebKeyEncryptionAlgorithm ",
          },
        ],
        isExported: true,
        name: "KnownEncryptionAlgorithms",
        releaseTag: ReleaseTag.Public,
      });
      entryPoint.addMember(apiEnum);
      apiPackage.addMember(entryPoint);
      model.addMember(apiPackage);
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
      const enumReviewLine = result.ReviewLines[0].Children?.[0];
      expect(enumReviewLine).toBeDefined();
      expect(enumReviewLine!.Tokens.map((t) => t.Value).join(" ")).toEqual(
        "export enum KnownEncryptionAlgorithms { }",
      );
    });
  });
});
