import { apiViewFor, apiViewText, compare } from "./test-host.js";
import { CodeFile, ReviewLine } from "../src/schemas.js";
import { describe, it } from "vitest";
import { fail } from "assert";

interface ReviewLineData {
  prefixCount: number;
  suffixCount: number;
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
  function validateRelatedLines(apiview: CodeFile, data: Map<string, ReviewLineData>) {
    const lineIdsFound = new Set<string>();

    function validateReviewLines(lines: ReviewLine[] | undefined) {
      if (lines === undefined || lines.length === 0) return;
      lines.forEach((line, index) => {
        const lineId = line.LineId;
        const related = line.RelatedToLine;
        // check for a closing } IsConextEndLine
        if (lineId !== undefined && lineId !== "") {
          lineIdsFound.add(lineId);
          const next = lines[index + 1];
          let meta = data.get(lineId);
          if (meta) {
            if (next?.IsContextEndLine) {
              meta.suffixCount--;
            }
          }
        }
        // check is this is a prefix line
        if (related !== undefined) {
          let meta = data.get(related);
          if (meta === undefined) {
            return;
          }
          if (!lineIdsFound.has(related)) {
            meta.prefixCount--;
          }
        }
        validateReviewLines(line.Children);
      });
    }
    
    validateReviewLines(apiview.ReviewLines);
    // verify that all counts are 0
    const keysToRemove = [];
    for (const [lineId, meta] of data) {
      if (meta.prefixCount == 0 && meta.suffixCount == 0) {
        keysToRemove.push(lineId);
      }
    }
    for (const key of keysToRemove) {
      data.delete(key);
    }
    if (data.size > 0) {
      fail(`Related line mismatches found!: ${JSON.stringify(Object.fromEntries(data))}`);
    }
  }
  

  describe("models", () => {
    it("composition", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Animal", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Cat", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Dog", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Pet", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Pig", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });

    it("templated", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.ConstrainedComplex", { prefixCount: 0, suffixCount: 2 }],
        ["Azure.Test.ConstrainedSimple", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.ConstrainedWithDefault", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.NamedStringThing", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.Page", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.StringPage", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.StringThing", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.Thing", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("with default values", async () => {
      const input = `
        #suppress "deprecated"
        @TypeSpec.service( { title: "Test", version: "1" } )
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
            obj: Record<unknown> = #{val: 1, name: "foo"};
          }
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Foo", { prefixCount: 0, suffixCount: 1 }],
      ]));

    });
  });

  describe("scalars", () => {
    it("extends string", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Password", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });

    it("new scalar type", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.ternary", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });

    it("templated", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Unreal", { prefixCount: 1, suffixCount: 0 }],
      ]));
    });
  });

  describe("aliases", () => {
    it("simple alias", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Animal", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Creature", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });

    it("templated alias", async () => {
      const input = `
        #suppress "deprecated"
        @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Animal", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Template", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });
  });

  describe("augment decorators", () => {
    it("simple augment", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Animal", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.@@doc.Animal", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });
  });

  describe("enums", () => {
    it("literal labels", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SomeEnum", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("string-backed values", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SomeStringEnum", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("int-backed values", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SomeIntEnum", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("spread labels", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SomeEnum", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.SomeSpreadEnum", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });
  });

  describe("unions", () => {
    it("discriminated union", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Cat", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Dog", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.MyUnion", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Snake", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("unnamed union", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Cat", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Dog", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Animals", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Snake", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });
  });

  describe("operations", () => {
    it("templated with simple types", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.GetFoo", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.NamedGetFoo", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.ResourceRead", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("templated with deeply nested models", async () => {
      const input = `
      #suppress "deprecated"
      @service({name: "Service", version: "1"})
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Foo", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.Temp", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("templated with model types", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.GetFoo", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.NamedGetFoo", { prefixCount: 1, suffixCount: 0 }],
        ["Azure.Test.ResourceRead", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.FooParams", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("templated with mixed types", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.GetFoo", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.NamedGetFoo", { prefixCount: 1, suffixCount: 0 }],
        ["Azure.Test.ResourceRead", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.FooParams", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("templated with empty models", async () => {
      const input = `
        #suppress "deprecated"
        @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.GetFoo", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.NamedGetFoo", { prefixCount: 1, suffixCount: 0 }],
        ["Azure.Test.ResourceRead", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.FooParams", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });

    it("with anonymous models", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SomeOp", { prefixCount: 0, suffixCount: 1 }],
      ]));
    });
  });

  describe("interfaces", () => {
    it("simple interface", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Foo", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.get", { prefixCount: 2, suffixCount: 1 }],
        ["Azure.Test.list", { prefixCount: 2, suffixCount: 0 }],
      ]));
    });
  });

  describe("string literals", () => {
    it("long strings", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Bar", { prefixCount: 5, suffixCount: 0 }],
      ]));
    });

    it("short strings", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Foo", { prefixCount: 1, suffixCount: 0 }],
      ]));
    });
  });

  describe("string templates", () => {
    it("templates", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Person", { prefixCount: 0, suffixCount: 1 }],
        ["Azure.Test.myconst", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.Template", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });
  });

  describe("suppressions", () => {
    it("suppression on model", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.Foo", { prefixCount: 2, suffixCount: 1 }],
      ]));
    });

    it("suppression on namespace", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.SubNamespace", { prefixCount: 2, suffixCount: 1 }],
      ]));
    });

    it("suppression on operation", async () => {
      const input = `
        #suppress "deprecated"
        @TypeSpec.service( { title: "Test", version: "1" } )
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
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.someOp", { prefixCount: 1, suffixCount: 0 }],
      ]));
    });
  });

  describe("constants", () => {
    it("renders constants", async () => {
      const input = `
      #suppress "deprecated"
      @TypeSpec.service( { title: "Test", version: "1" } )
      namespace Azure.Test {
        const a = 123;
        const b = #{name: "abc"};
        const c = a;
      }
      `;
      const expect = `
        namespace Azure.Test {
          const a = 123;
          const b = #{name: "abc"};
          const c = a;
        }
        `;
      const apiview = await apiViewFor(input, {});
      const actual = apiViewText(apiview);
      compare(expect, actual, 10);
      validateLineIds(apiview);
      validateRelatedLines(apiview, new Map([
        ["Azure.Test", { prefixCount: 3, suffixCount: 1 }],
        ["Azure.Test.a", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.b", { prefixCount: 0, suffixCount: 0 }],
        ["Azure.Test.c", { prefixCount: 0, suffixCount: 0 }],
      ]));
    });
  });
});
