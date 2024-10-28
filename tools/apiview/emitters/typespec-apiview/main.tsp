@service({name: "Service", version: "1"})
namespace Azure.Test {

  op GetFoo is ResourceRead<
    {
      @doc("The name")
      name: string;
    },
    {
      parameters:
        {
          @doc("The collection id.")
          fooId: string;
        };
    }
  >;

  // op GetFoo is ResourceRead<
  //   string,
  //   {
  //     parameters:
  //       {
  //         @doc("The collection id.")
  //         fooId: string;
  //       };
  //   }
  // >;

  // op GetFoo is ResourceRead<string, string>;

  op ResourceRead<TResource, TParams>(
    resource: TResource,
    params: TParams
  ): TResource;
}