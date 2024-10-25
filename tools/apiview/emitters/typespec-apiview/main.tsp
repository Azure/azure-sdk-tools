#suppress "deprecated"
@TypeSpec.service( { title: "Test", version: "1" } )
namespace Azure.Test {
  model Foo {};
}

namespace Azure.Test.Sub {
  model SubFoo {};
};

namespace Azure.TestBad {
  model BadFoo {};
};
