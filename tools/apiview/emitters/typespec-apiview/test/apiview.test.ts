import { apiViewFor, apiViewText, compare } from "./test-host.js";
import { CodeFile, ReviewLine } from "../src/schemas.js";
import { describe, it } from "vitest";
import { fail } from "assert";
import { isDeepStrictEqual } from "util";

interface ReviewLineData {
  relatedToCount: number;
  isContextEndCount: number;
}

describe("apiview: tests", () => {

  function validateReviewLineIds(definitionIds: Set<string>, line: ReviewLine) {
    // ensure that there are no repeated definition IDs.
    if (line.LineId !== undefined) {
      if (definitionIds.has(line.LineId)) {
        fail(`Duplicate defintion ID ${line.LineId}.`);
      }
      if (line.LineId !== "") {
        definitionIds.add(line.LineId);
      }
      for (const child of line.Children) {
        validateReviewLineIds(definitionIds, child);
      }
    }
  }

  /** Validates that there are no repeat defintion IDs. */
  function validateLineIds(apiview: CodeFile) {
    const definitionIds = new Set<string>();
    for (const line of apiview.ReviewLines) {
      validateReviewLineIds(definitionIds, line);
    }
  }

  /** Validates that related lines point to a valid line. */
  function getRelatedLineMetadata(apiview: CodeFile): Map<string, ReviewLineData> {

    function getReviewLinesMetadata(lines: ReviewLine[] | undefined): Map<string, ReviewLineData> | undefined {
      if (lines === undefined || lines.length === 0) return undefined;
      const mainMap = new Map<string, ReviewLineData>();
      let lastKey: string | undefined = undefined;
      for (const line of lines) {
        const related = line.RelatedToLine;
        const lineId = line.LineId
        const isEndContext = line.IsContextEndLine;
        if (related) {
          lastKey = related;
          if (!mainMap.has(related)) {
            mainMap.set(related, { relatedToCount: 0, isContextEndCount: 0 });
          }
          mainMap.get(related)!.relatedToCount++;
        }
        if (isEndContext) {
          if (lastKey === undefined) {
            fail("isEndContext without a related line.");
          }
          if (!mainMap.has(lastKey)) {
            mainMap.set(lastKey, { relatedToCount: 0, isContextEndCount: 0 });
          }
          mainMap.get(lastKey)!.isContextEndCount++;
        }
        if (line.Children?.length > 0) {
          if (lineId === undefined) {
            fail("Children without a line ID.");
          }
          lastKey = lineId;
          const childMap = getReviewLinesMetadata(line.Children);
          if (childMap !== undefined && childMap.size > 0) {
            for (const [key, value] of childMap) {
              mainMap.set(key, value);
            }
          }
        }
      }
      return mainMap;
    }
    const countMap = getReviewLinesMetadata(apiview.ReviewLines);
    return countMap ?? new Map<string, ReviewLineData>();
  }
  
  function compareCounts(lhs: Map<string, ReviewLineData>, rhs: Map<string, ReviewLineData>) {
    // ensure the keys are the same
    const lhsKeys = new Set([...lhs.keys()]);
    const rhsKeys = new Set([...rhs.keys()]);
    const combined = new Set([...lhsKeys, ...rhsKeys]);
    if (combined.size != lhsKeys.size) {
      fail(`Keys mismatch: ${JSON.stringify([...lhsKeys])} vs ${JSON.stringify([...rhsKeys])}`);
    }
    isDeepStrictEqual(lhs, rhs);
  }


  describe("models", () => {
    it("composition", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model Animal {
          species: string;
        }
  
        model Pet {
          name?: string;
        }
  
        model Dog {
          ...Animal;
          ...Pet;
        }
  
        model Cat {
          species: string;
          name?: string = "fluffy";
        }
  
        model Pig extends Animal {}
      }
      `;
      const expect = `
      namespace Azure.Test {
        model Animal {
          species: string;
        }

        model Cat {
          species: string;
          name?: string = "fluffy";
        }

        model Dog {
          ...Animal;
          ...Pet;
        }

        model Pet {
          name?: string;
        }

        model Pig extends Animal {}
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Animal", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Cat", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Dog", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Pet", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model Thing<T> {
          property: T;
        }
  
        model StringThing is Thing<string>;
  
        model NamedStringThing is Thing<T = string>;

        model Page<T = string> {
          size: int16;
          item: T[];
        }
  
        model StringPage {
          ...Page<int16>;
        }
  
        model ConstrainedSimple<X extends string> {
          prop: X;
        }
  
        model ConstrainedComplex<X extends {name: string}> {
          prop: X;
        }
        
        model ConstrainedWithDefault<X extends string = "abc"> {
          prop: X;
        }
      }
      `;
      const expect = `
      namespace Azure.Test {
        model ConstrainedComplex<X extends
          {
            name: string;
          }
        > {
          prop: X;
        }
  
        model ConstrainedSimple<X extends string> {
          prop: X;
        }
  
        model ConstrainedWithDefault<X extends string = "abc"> {
          prop: X;
        }
  
        model NamedStringThing is Thing<T = string> {}

        model Page<T = string> {
          size: int16;
          item: T[];
        }
  
        model StringPage {
          ...Page<int16>;
        }
  
        model StringThing is Thing<string> {}

        model Thing<T> {
          property: T;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.ConstrainedComplex", { relatedToCount: 1, isContextEndCount: 1 }],
        ["Azure.Test.ConstrainedSimple", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.ConstrainedWithDefault", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Page", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.StringPage", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Thing", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("with default values", async () => {
      const input = `
        @TypeSpec.service( #{ title: "Test" } )
        namespace Azure.Test {
          model Foo {
            name: string = "foo";
            array: string[] = #["a", "b"];
            obj: Record<unknown> = #{val: 1, name: "foo"};
          }
        }
        `;
      const expect = `
        namespace Azure.Test {
          model Foo {
            name: string = "foo";
            array: string[] = #["a", "b"];
            obj: Record<unknown> = #{
              val: 1,
              name: "foo"
            };
          }
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Foo", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));

    });
  });

  describe("scalars", () => {
    it("extends string", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        scalar Password extends string;
      }
      `;
      const expect = `
      namespace Azure.Test {
        scalar Password extends string
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
      ]));
    });

    it("new scalar type", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        scalar ternary;
      }
      `;
      const expect = `
      namespace Azure.Test {
        scalar ternary
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
      ]));
    });

    it("templated", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        @doc(T)
        scalar Unreal<T extends valueof string>;
      }
      `;
      const expect = `
      namespace Azure.Test {
        @doc(T)
        scalar Unreal<T extends valueof string>
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Unreal", { relatedToCount: 1, isContextEndCount: 0 }],
      ]));
    });
  });

  describe("aliases", () => {
    it("simple alias", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model Animal {
          species: string;
        }
  
        alias Creature = Animal;
      }
      `;
      const expect = `
      namespace Azure.Test {
        model Animal {
          species: string;
        }
  
        alias Creature = Animal;
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Animal", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated alias", async () => {
      const input = `
        @TypeSpec.service( #{ title: "Test" } )
        namespace Azure.Test {
          model Animal {
            species: string;
          }

          alias Template<T extends string> = "Foo \${T} bar";
        }
        `;
      const expect = `
        namespace Azure.Test {
          model Animal {
            species: string;
          }
    
          alias Template<T extends string> = "Foo \${T} bar";
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Animal", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("augment decorators", () => {
    it("simple augment", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model Animal {
          species: string;
        }
  
        @@doc(Animal, "My doc");
      }
      `;
      const expect = `
      namespace Azure.Test {
        @@doc(Animal, "My doc")
  
        model Animal {
          species: string;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Animal", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("enums", () => {
    it("literal labels", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        enum SomeEnum {
          Plain,
          "Literal",
        }
      }`;
      const expect = `
      namespace Azure.Test {
        enum SomeEnum {
          Plain,
          "Literal",
        }
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SomeEnum", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("string-backed values", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        enum SomeStringEnum {
          A: "A",
          B: "B",
        }
      }`;
      const expect = `
      namespace Azure.Test {
        enum SomeStringEnum {
          A: "A",
          B: "B",
        }
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SomeStringEnum", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("int-backed values", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        enum SomeIntEnum {
          A: 1,
          B: 2,
        }
      }`;
      const expect = `
      namespace Azure.Test {
        enum SomeIntEnum {
          A: 1,
          B: 2,
        }
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SomeIntEnum", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("spread labels", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
  
        enum SomeEnum {A}
  
        enum SomeSpreadEnum {...SomeEnum}
      }`;
      const expect = `
      namespace Azure.Test {
        enum SomeEnum {
          A,
        }
  
        enum SomeSpreadEnum {
          ...SomeEnum,
        }
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SomeEnum", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.SomeSpreadEnum", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("unions", () => {
    it("discriminated union", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        union MyUnion {
          cat: Cat,
          dog: Dog,
          snake: Snake
        }
  
        model Cat {
          name: string;
        }
  
        model Dog {
          name: string;
        }
  
        model Snake {
          name: string;
          length: int16;
        }
      }`;
      const expect = `
      namespace Azure.Test {
        model Cat {
          name: string;
        }
  
        model Dog {
          name: string;
        }
  
        union MyUnion {
          cat: Cat,
          dog: Dog,
          snake: Snake
        }
  
        model Snake {
          name: string;
          length: int16;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Cat", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Dog", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.MyUnion", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Snake", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("unnamed union", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        union Animals { Cat, Dog, Snake };
  
        model Cat {
          name: string;
        }
  
        model Dog {
          name: string;
        }
  
        model Snake {
          name: string;
          length: int16;
        }
      }`;
      const expect = `
      namespace Azure.Test {
        union Animals {
          Cat,
          Dog,
          Snake
        }

        model Cat {
          name: string;
        }
  
        model Dog {
          name: string;
        }
    
        model Snake {
          name: string;
          length: int16;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Cat", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Dog", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Animals", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Snake", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("operations", () => {
    it("templated with simple types", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;
  
        op GetFoo is ResourceRead<string, string>;

        @route("/named")
        op NamedGetFoo is ResourceRead<TResource = string, TParams = string>;
      }`;
      const expect = `
      namespace Azure.Test {
        op GetFoo is ResourceRead<string, string>;

        @route("/named")
        op NamedGetFoo is ResourceRead<TResource = string, TParams = string>;

        op ResourceRead<TResource, TParams>(
          resource: TResource,
          params: TParams
        ): TResource;
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.NamedGetFoo", { relatedToCount: 1, isContextEndCount: 0 }],
        ["Azure.Test.ResourceRead", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated with deeply nested models", async () => {
      const input = `
      @service(#{title: "Service"})
      namespace Azure.Test {
        op Foo is Temp< {
          parameters: {
            fooId: {
              bar: {
                baz: {
                  qux: string;
                };
              };
            };
          };
        }>;
        
        op Temp<T>(
          params: T
        ): void;
      }`;
      const expect = `
      namespace Azure.Test {
        op Foo is Temp<
          {
            parameters:
              {
                fooId:
                  {
                    bar:
                      {
                        baz:
                          {
                            qux: string;
                          };
                      };
                  };
              };
          }
        >;
        
        op Temp<T>(
          params: T
        ): void;
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Foo", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Temp", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated with model types", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model FooParams {
          a: string;
          b: string;
        }
  
        op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;
  
        op GetFoo is ResourceRead<
          {
            @query
            @doc("The name")
            name: string,
            ...FooParams
          },
          {
            parameters: {
              @query
              @doc("The collection id.")
              fooId: string
            };
          }
        >;

        @route("/named")
        op NamedGetFoo is ResourceRead<
          TResource = {
            @query
            @doc("The name")
            name: string,
            ...FooParams
          },
          TParams = {
            parameters: {
              @query
              @doc("The collection id.")
              fooId: string
            };
          }
        >;
      }`;
      const expect = `
      namespace Azure.Test {
        op GetFoo is ResourceRead<
          {
            @query
            @doc("The name")
            name: string;
            ...FooParams;
          },
          {
            parameters:
              {
                @query
                @doc("The collection id.")
                fooId: string;
              };
          }
        >;

        @route("/named")
        op NamedGetFoo is ResourceRead<
          TResource = {
            @query
            @doc("The name")
            name: string;
            ...FooParams;
          },
          TParams = {
            parameters:
              {
                @query
                @doc("The collection id.")
                fooId: string;
              };
          }
        >;

        op ResourceRead<TResource, TParams>(
          resource: TResource,
          params: TParams
        ): TResource;
  
        model FooParams {
          a: string;
          b: string;
        }
      }`;
      // Related line mismatches found!: {"Azure.Test.NamedGetFoo":{"relatedToCount":1,"isContextEndCount":-1}}
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.GetFoo", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.NamedGetFoo", { relatedToCount: 1, isContextEndCount: 1 }],
        ["Azure.Test.ResourceRead", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.FooParams", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated with mixed types", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        model FooParams {
          a: string;
          b: string;
        }
  
        op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;
  
        op GetFoo is ResourceRead<
          string,
          {
            parameters: {
              @query
              @doc("The collection id.")
              fooId: string
            };
          }
        >;

                @route("/named")
        op NamedGetFoo is ResourceRead<
          TResource = string,
          TParams = {
            parameters:
              {
                @query
                @doc("The collection id.")
                fooId: string;
              };
          }
        >;
      }`;
      const expect = `
      namespace Azure.Test {
        op GetFoo is ResourceRead<
          string,
          {
            parameters:
              {
                @query
                @doc("The collection id.")
                fooId: string;
              };
          }
        >;

        @route("/named")
        op NamedGetFoo is ResourceRead<
          TResource = string,
          TParams = {
            parameters:
              {
                @query
                @doc("The collection id.")
                fooId: string;
              };
          }
        >;

        op ResourceRead<TResource, TParams>(
          resource: TResource,
          params: TParams
        ): TResource;
  
        model FooParams {
          a: string;
          b: string;
        }
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.GetFoo", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.NamedGetFoo", { relatedToCount: 1, isContextEndCount: 1 }],
        ["Azure.Test.ResourceRead", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.FooParams", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });

    it("templated with empty models", async () => {
      const input = `
        @TypeSpec.service( #{ title: "Test" } )
        namespace Azure.Test {
    
          op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;
    
          op GetFoo is ResourceRead<{}, {}>;
  
          @route("/named")
          op NamedGetFoo is ResourceRead<
            TResource = {},
            TParams = {}
          >;
        }`;
      const expect = `
        namespace Azure.Test {
          op GetFoo is ResourceRead<
            {},
            {}
          >;
  
          @route("/named")
          op NamedGetFoo is ResourceRead<
            TResource = {},
            TParams = {}
          >;
  
          op ResourceRead<TResource, TParams>(
            resource: TResource,
            params: TParams
          ): TResource;
        }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.GetFoo", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.NamedGetFoo", { relatedToCount: 1, isContextEndCount: 1 }],
        ["Azure.Test.ResourceRead", { relatedToCount: 0, isContextEndCount: 1 }]
      ]));
    });

    it("with anonymous models", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        op SomeOp(
          param1: {
            name: string
          },
          param2: {
            age: int16
          }
        ): string;
      }`;
      const expect = `
      namespace Azure.Test {
        op SomeOp(
          param1:
            {
              name: string;
            },
          param2:
            {
              age: int16;
            }
        ): string;
      }`;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SomeOp", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("interfaces", () => {
    it("simple interface", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        interface Foo {
          @get 
          @route("get/{name}")
          get(@path name: string): string;
  
          @get
          @route("list")
          list(): string[];
        }
      }
      `;
      const expect = `
      namespace Azure.Test {
        interface Foo {
          @get
          @route("get/{name}")
          get(
            @path
            name: string
          ): string;

          @get
          @route("list")
          list(): string[];
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Foo", { relatedToCount: 0, isContextEndCount: 1 }],
        ["Azure.Test.Foo.get", { relatedToCount: 2, isContextEndCount: 1 }],
        ["Azure.Test.Foo.list", { relatedToCount: 2, isContextEndCount: 0 }],
      ]));
    });
  });

  describe("string literals", () => {
    it("long strings", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        @doc("""
        A long string,
        with line breaks
        and stuff...
        """)
        model Bar {};
      }
      `;
      const expect = `
      namespace Azure.Test {
        @doc("""
        A long string,
        with line breaks
        and stuff...
        """)
        model Bar {}
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Bar", { relatedToCount: 5, isContextEndCount: 0 }],
      ]));
    });

    it("short strings", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        @doc("Short string")
        model Foo {};  
      }
      `;
      const expect = `
      namespace Azure.Test {
        @doc("Short string")
        model Foo {}
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Foo", { relatedToCount: 1, isContextEndCount: 0 }],
      ]));
    });
  });

  describe("string templates", () => {
    it("templates", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        alias myconst = "foobar";
        model Person {
          simple: "Simple \${123} end";
          multiline: """
            Multi
              \${123}
              \${true}
            line
          """;
          ref: "Ref this alias \${myconst} end";
          template: Template<"custom">;          
        }
        alias Template<T extends string> = "Foo \${T} bar";
      }`;

      const expect = `
      namespace Azure.Test {
        model Person {
          simple: "Simple \${123} end";
          multiline: """
              Multi
                \${123}
                \${true}
              line
          """;
          ref: "Ref this alias \${myconst} end";
          template: Template<"custom">;
        }

        alias myconst = "foobar";

        alias Template<T extends string> = "Foo \${T} bar";
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Person", { relatedToCount: 0, isContextEndCount: 1 }],
      ]));
    });
  });

  describe("suppressions", () => {
    it("suppression on model", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        #suppress "foo" "bar"
        @doc("Foo Model")
        model Foo {
          name: string;
        }
      }
      `;
      const expect = `
      namespace Azure.Test {
        #suppress "foo" "bar"
        @doc("Foo Model")
        model Foo {
          name: string;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.Foo", { relatedToCount: 2, isContextEndCount: 1 }],
      ]));
    });

    it("suppression on namespace", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {  
        #suppress "foo" "bar"
        @doc("SubNamespace")
        namespace SubNamespace {
          model Blah {
            name: string;
          }      
        }
      }
      `;
      const expect = `
      namespace Azure.Test {
      }

      #suppress "foo" "bar"
      @doc("SubNamespace")
      namespace Azure.Test.SubNamespace {
        model Blah {
          name: string;
        }
      }
      `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.SubNamespace", { relatedToCount: 2, isContextEndCount: 1 }],
      ]));
    });

    it("suppression on operation", async () => {
      const input = `
        @TypeSpec.service( #{ title: "Test" } )
        namespace Azure.Test {
            #suppress "foo" "bar"
            op someOp(): void;
        }
        `;
      const expect = `
        namespace Azure.Test {
          #suppress "foo" "bar"
          op someOp(): void;
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
        ["Azure.Test.someOp", { relatedToCount: 1, isContextEndCount: 0 }],
      ]));
    });
  });

  describe("constants", () => {
    it("renders constants", async () => {
      const input = `
      @TypeSpec.service( #{ title: "Test" } )
      namespace Azure.Test {
        const a = 123;
        const b = #{name: "abc"};
        const c = a;
      }
      `;
      const expect = `
        namespace Azure.Test {
          const a = 123;
          const b = #{
            name: "abc"
          };
          const c = a;
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 6);
      validateLineIds(apiview);
      const counts = getRelatedLineMetadata(apiview);
      compareCounts(counts, new Map([
        ["Azure.Test", { relatedToCount: 3, isContextEndCount: 1 }],
      ]));
    });
  });

  it("renders examples with call expression constants", async () => {
    const input = `
    @TypeSpec.service( #{ title: "Test" } )
    namespace Azure.Test {
      const SomeExample: SomeData = #{
        timestamp: utcDateTime.fromISO("2020-12-09T13:50:19.9995668-08:00"),
        name: "test"
      };
      
      @example(SomeExample)
      model SomeData {
        timestamp: utcDateTime;
        name: string;
      }     
    }
    `;
    const expect = `
      namespace Azure.Test {
        @example(SomeExample)
        model SomeData {
          timestamp: utcDateTime;
          name: string;
        }

        const SomeExample = #{
          timestamp: utcDateTime.fromISO("2020-12-09T13:50:19.9995668-08:00"),
          name: "test"
        };
      }
      `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 6);
    validateLineIds(apiview);
    const counts = getRelatedLineMetadata(apiview);
    compareCounts(counts, new Map([
      ["Azure.Test", { relatedToCount: 1, isContextEndCount: 1 }],
    ]));
  });
});
