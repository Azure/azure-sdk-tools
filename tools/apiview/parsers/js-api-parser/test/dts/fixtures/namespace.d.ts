// namespace.d.ts — nested namespace declarations.

export declare namespace Outer {
  function outerFn(): void;

  interface OuterInterface {
    value: string;
  }

  namespace Inner {
    function innerFn(x: number): boolean;

    type InnerAlias = string | number;

    namespace DeepNested {
      const DEEP_CONST: string;
    }
  }
}
