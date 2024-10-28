import { apiViewFor, apiViewText, compare } from "./test-host.js";
import { CodeFile } from "../src/schemas.js";
import { describe, it } from "vitest";

describe("apiview: tests", () => {
  /** Validates that there are no repeat defintion IDs. */
  function validateLineIds(apiview: CodeFile) {
    return;
    // FIXME: Re-enable these once the syntax renders correctly.
    // const definitionIds = new Set<string>();
    // for (const line of apiview.ReviewLines) {
    //   // ensure that there are no repeated definition IDs.
    //   if (line.LineId !== undefined) {
    //     if (definitionIds.has(line.LineId)) {
    //       fail(`Duplicate defintion ID ${line.LineId}.`);
    //     }
    //     definitionIds.add(line.LineId);
    //   }
    // }
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
    });
  });
});
