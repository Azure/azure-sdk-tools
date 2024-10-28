#suppress "deprecated"
@service({name: "Service", version: "1"})
namespace Azure.Test {

  op MyTemp is Temp<
    {
      parameters: {
        name: string;
      }
    }
  >;

  op Temp<T>(
    type: T
  ): void;
}