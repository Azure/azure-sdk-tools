# Want to change @JsonProperty property in the java model which is being created from TypeSpec.

## Question  
So basically as the title suggests. I looked up some decorators docs to see if I can use those to modify what property is being set in @JsonProperty in the Java file for various fields. But the example used was of console.log. The only way I found to set some metadata to the decorators was through stateMap or stateSet which uses key. But I don't think any key is required in my scenario. Is there any way to change @JsonProperty from the field name when creating a model ?

## Answer
I am not sure whether I get the question.

I assume you are talking about a property with name e.g. "foo" in TypeSpec, but you want @JsonProperty("bar") instead for it.

There is an @encodedName decorator that modifies the "wire name" (the name of the property in the JSON payload).
E.g. https://github.com/Azure/typespec-azure/blob/main/packages/azure-http-specs/specs/client/naming/main.tsp#L37-L38
It means, when serialize/de-serialize for "application/json" content-type, use "wireName" as JSON property name, instead of "defaultName".

Note that the latest version of the http-client-java emitter no longer use Jackson (hence no @JsonProperty).

# Any plans to support description and external docs for the tag decorator?

## Question  
I've been using Redoc to render an OpenApi spec, and it would be nice if typespec could output the additional tag properties to the generated spec.

Currently, the tag decorator accepts only a `string`, but OAS 3 supports `description` and `externalDocs` in the root tag array (both are optional).

Are there any plans to support those properties? I noticed the decorator is exported from the standard library, so it may have other use cases I'm unaware of, but it would be a nice addition for this specific use case.

## Answer
We have an issue [#2220](https://github.com/microsoft/typespec/issues/2220). I believe someone was going to work on it soon.

# Better example tooling

## Question  
Many API developer portals (such as [readme.com](https://readme.com/)) make heavy use of examples, and it's a huge boon to have compile-type checking on them. But writing them is a bit hard.

I can think of two main places examples can come from:

Payload first: you already have some JSON (for example, if you're documenting an existing API) and you want to make sure your TypeSpec definition conforms.
In this case, the migration from e.g. `{"my array": []}` to `#{`my array`: #[]}` makes this super tedious in bulk (see [Value blocks for simplifying defining nested values #3931](https://github.com/microsoft/typespec/discussions/3931)). It would be a huge help if there was a CLI command that could take arbitrary JSON and spit out equivalent "value"s.
Model first: you have a TypeSpec model and are trying to write a definition for it. Unfortunately, there is no templating of the example decorator, so while there is realtime method validation, intellisense gives you no help (asides from general string matching) in defining the example:
![alt text](image.png)
It would be great if the type of @example : example could be an inferred T instead of just unknown.
And even after you have examples, the differing tooltip and error messages between different ways of specifying an example is really confusing and sometimes incredibly verbose [[Playground]](https://typespec.io/playground?c=Y29uc3QgZm9vZ29vZDEgOiBGb28gPSAje0JhcjrFB3o6ICLFHSJ9fTsNCs0xMiA6IMQqxjhCYXJ91jwy0zwz1iszzytiYfUAl2TFMcQd0jD%2FAJbJO%2FAAlmJh7wCVySrnAJUNCkBleGFtcGxlKOgBMinSFDLTFDPMFPMA9jTEYc845ADd0hPQXuQAntZd6QC%2ByF1tb2RlbOUBSnsNCiAg7AEiOw0KfcshxBvHJHogOiBzdHJpbmfEJA%3D%3D&e=%40typespec%2Fopenapi3&options=%7B%7D). So much so, I'm not sure I have a concrete "ask" (except [#4544](https://github.com/microsoft/typespec/issues/4544) and [#4545](https://github.com/microsoft/typespec/issues/4545)).

These thoughts are based on the work migrating the Sefaria spec; they have hundreds of examples which (see [#3866](https://github.com/microsoft/typespec/issues/3866)) were manually migrated.

## Answer
The main issue discussed revolves around improving the handling of examples in API documentation, particularly when migrating JSON data into TypeSpec definitions. There are two primary scenarios:

Payload First: Migrating existing JSON to TypeSpec models is tedious, especially with the need to manually add # for nested values. A CLI tool that automatically converts JSON to equivalent "value"s would be very helpful.

Model First: When creating examples for TypeSpec models, there is no templating for the @example decorator, which makes defining examples harder. It would be beneficial if the type for @example could be inferred instead of being unknown, and the differing error messages and tooltips can be confusing.

A key issue is that when converting an example like @example({"Baz": "Hello"}) into TypeSpec format, it is not enough to just add the #. The property Baz needs to be correctly recognized and mapped to the model type. This issue has been filed under issues [#4612](https://github.com/microsoft/typespec/issues/4612) and [#4613](https://github.com/microsoft/typespec/issues/4613).

The overall goal is to streamline the process of creating and validating examples, making it easier for developers to generate and maintain accurate API documentation.

# Query about key decorator @key

## Question  
Hi,
I want to know the usage of key decorator(@key).
I have tried to add @key to 'id' property in my model and compiled with 2 emitters below. But the result is the same with model without @key. So it seems @key doesn't take effect in my case, why?

@azure-tools/typespec-ts
@typespec/openapi3

`model MyCalculationOutput {
@key
id: string;
processDate: plainDate;
result: string;
}

@get
@route("/$calculation")
op calculation(): MyCalculationOutput[] ;`

Thanks

The background is: The 'calculation' method returns a list of records. I want this list of records could be presented as a dictionary/map of records with the 'id' of each record used as the dictionary/map key

## Answer
Key conceptually flags the fields which identify the resource, but doesn't imply any special behavior for that type within an array. You can maybe get what you want by doing:
```
op calculation(): Record<MyCalculationOutput>
```

# Can't generate multiple services imported in the main.tsp

## Question  
Have been stuck for a while on this problem: every time I'm trying to emit a yaml file by running npx tsp compile  . I get generated a yaml file only for the last imported service. Here's my tspconfig.yaml :
```
  - "@typespec/openapi3"

options:
  "@typespec/openapi3":
    "output-file": "openapi.yaml"
```
I don't know if it has anything to do with it but I also was getting warnings during compilation ln terminal: No namespace with '@service' was found, but Namespace 'AdminService' contains routes. Did you mean to annotate this with '@service'?  which I found unreasonable, since I have service decorators everywhere, and added #suppress "@typespec/http/no-service-found" "" to supress it. Here's the repository I'm having issues with.

## Answer
The problem was caused by a conflict between the global and local versions of the TypeSpec compiler. Once the user reinstalled the template and corrected the setup, the issue was resolved, and all services could be compiled into the YAML file as expected. The incorrect usage of using was also clarified, with the suggestion to nest namespaces properly under the main service (e.g., `namespace ECommerce.AuthService;`).

# How to emit option

## Question  
Hey 👋
I have an existing protobuf definition which I want to port to typespec, but I don't see how I can implement option:
```
// name/backstage/service_options.proto file
syntax = "proto3";

package name.backstage;

import "google/protobuf/descriptor.proto";

option go_package = "github.com/name/repo/proto/gen/go/name/backstage";
option java_package = "com.name.backstage";

extend google.protobuf.ServiceOptions {
  // Used to fill the spec.owner field in a backstage file
  optional string owner = 51001;
  // Used to fill the system field in a backstage file
  optional string system = 51002;
}

// somewhere in proto files
import "name/backstage/service_options.proto";

service DemoService {
    option (name.backstage.owner) = "team/demo-team";

    rpc GetData(GetDataRequest) returns (GetDataResponse) {
        option (google.api.http) = {
            get: "/v1/api-name/{param}/data"
          };
    }
}
```
Using using google http annotations is our strict requirement and I can't find a way to do it with typespec.
https://docs.solo.io/gloo-edge/latest/reference/api/github.com/solo-io/solo-kit/api/external/google/api/http.proto.sk/

## Answer
The answer is this feature request [#4090](https://github.com/microsoft/typespec/issues/4090)

# How to DRYup commonly-used extension annotations?

## Question  
Hey folks. I'm working on hooking up our TypeSpec -> OpenAPI pipeline to generating SDKs with Speakeasy. As part of that, we have to add extension annotations to all of our endpoints that respond with paginated results, e.g.:
```
            // All operations must have a summary.
            @summary("List all alert definitions")
            @extension("x-speakeasy-pagination", {
                type: "url",
                outputs: {
                  nextUrl: "$.pageInfo.next",
                }
            })
            op list(@query skipToken?: string, @query pageSize?: int32): {
                @statusCode _: 200;
                @bodyRoot alertDefinitions: {
                    alertDefinitions: Definition[];
                    pageInfo?: PageInfo;
                };
            };
```
I'd like to make it so that teams don't have to remember or copypasta this extension annotation and others like it.

I know that I can at least replace "x-speakeasy-pagination" with a string constant, but because object constants are typed, I can't pass one in here as the second argument to extension.
```
Argument of type '#{type: "url", ref: "$.pageInfo.next"}' is not assignable to parameter of type 'unknown'TypeSpec(invalid-argument)
```
I was actually sort of surprised by this, but I am new to TypeScript as well.

I'd appreciate any guidance here. Thank you.
## Answer
I think the answer here is to create a custom decorator that does this for me. :)

# How to get specified version of Http services with 'getAllHttpServices'

## Question  
Hello team,

Currently we are using the function 'getAllHttpServices' from '@typespec/http' to extract http services from typespec files. Now we are planning to use the versioning decorators in those typespecs to manage versions. Will add 'api-version' in the emitter options as the target version.

I just want to check if 'getAllHttpServices' supports getting specified version of http services from those decorated typespecs (by giving any options?). If not, what is the appropriate way achieving that?

Thanks.
## Answer
hello, it seems I find a way for this. Seems I need to call 'createSdkContext' to re-create the context instead of using the passed in default context.

# Facing Error error @azure-tools/typespec-azure-resource-manager/template-type-constraint-no-met

## Question  
I'm trying to add an extension resource named approvals.
Here are my changes https://dev.azure.com/msazuredev/VirtualEnclaves/_git/ve-common/commit/2b8213089d00928031792d571aa6fc92cfe57cbc?refName=refs%2Fheads%2Fdrkapoor%2FtypespecChanges. I keep facing this error when i compile the changes, `@azure-tools/typespec-azure-resource-manager/template-type-constraint-no-met`
![alt text](image-1.png)
Needed help figuring out where I'm going wrong. Thanks

## Answer
For question about azure libraries please ask them in the Teams channel or file bug in this repo https://github.com/Azure/typespec-azure.

This error tells you the resource you are passing is not an arm resource. It should extend on of the TrackedResource, ProxyResource, etc. See [docs](https://azure.github.io/typespec-azure/docs/howtos/ARM/resource-type)

# @example for plainDate

## Question  
I'm trying out the new `@example` decorator and it works well from what I've seen but for `plainDate`/`offsetDateTime` data types.

For example, when I try to provide an example for a date field like this:
```
model Foo {
  @example("2020-12-12")
  bar: plainDate;
}
```
the TypeSpec compiler will error with this message:
```
error unassignable: Type '"2020-12-12"' is not assignable to type 'plainDate'
> 21 |   @example("2020-12-12")
```
Other date/time related examples are also failing, i.e. `@example("2022-09-15T14:15:00+08:00")` for a `offsetDateTime` property gives the same error.

What is the correct way of providing example date/time values?

## Answer
Oh, I think I found it right after posting 🤦

This seems to do the trick:
```
@example(plainDate.fromISO("2020-12-12"))
```
# Value marshalling in TypeSpec 0.57^

## Question  
I am working on upgrading our TypeSpec version to the latest version and fixing all breaking changes. However, I have a decorator with the alias '$flags'. This is causing conflicts when I apply the suggestion here:
```
Deprecated: Parameter name of decorator @function is using legacy marshalling but is accepting null as a type.
This will change in the future.
Add `export const $flags = {decoratorArgMarshalling: "new"}}` to your library to opt-in to the new marshalling behavior.
> 206 | extern dec function(target: Operation, name?: valueof string | null, isComposable?: valueof boolean);
```
Does the exported constant have to be $flags?

## Answer
Seems to be a limitation we have today filed an issue [#3719](https://github.com/microsoft/typespec/issues/3719)

# The compiler is added as dependency instead of peer dependency

## Question  
When you create a TypeSpec emitter with the template, @typespec/compiler is added as a dependency instead of a peer dependency. However, according to the [document](https://typespec.io/docs/extending-typespec/basics#step-3-defining-dependencies), you should

Use peerDependencies for all TypeSpec libraries (and the compiler) that you use in your own library or emitter.

Is this intended behavior or not?

**How to reproduce**
1.Install `@typespec/compiler@0.57.0`
2.Run `tsp init --template emitter-ts`
In the generated "package.json", `@typespec/compiler` is added as a dependency.

## Answer
This is a bug, and a corresponding issue has been created. [#3632](https://github.com/microsoft/typespec/issues/3632)

# Is it possible to "Pick" a subset of properties into a model?

## Question  
The usecase is that we have models that are used by different apis, sometimes with only a subset of the properties. For example, emitting CustomerCreated events to a webhook that only include id, name, email from Customer.

I could create BaseCustomer and have multiple things extend it, but maybe there's another way?

e.g.
```
model Widget {
  @key id: string;
  weight: int32;
  color: "red" | "blue";
}

model WidgetNoColor extends Pick<Widget, "id" | "weight"> {
}

model WidgetNoWeight extends Widget {
  weight: never;
}

model WidgetCreatedEvent {
  widgetId: Widget["id"]
}
```
## Answer
The user initially wanted `Pick` support in TypeSpec, similar to the existing `Omit` type. After discussing potential solutions like decorators or splitting models, it was confirmed that `Pick` was implemented and released in TypeSpec 0.57. The conversation also touched on the idea of supporting utility types like `Omit` and `Pick` as first-class types in TypeSpec, but there wasn’t a clear design for this at the time.

# Error in TypeSpec Code: duplicate-property: Model already has a property named

## Question  
When I write the code as below, I get an error duplicate-property: Model already has a property named countryCode.
Why is this happening?
```
import "@typespec/http";

using TypeSpec.Http;

@tag("Country")
@route("/countries")
interface Countries {
    @route("{countryCode}/update") @post update(@path countryCode: string, ...Country): void | Error;
}

@doc("Country")
model Country {
    @doc("countryCode")
    countryCode: string;
    @doc("countryName")
    countryName: string;
}
```
```
countries.tsp:8:76 - error duplicate-property: Model already has a property named countryCode
> 8 |     @route("{countryCode}/update") @post update(@path countryCode: string, ...Country): void | Error;
    |                                                                            ^^^^^^^^^^

Found 1 error.
```
Is it not possible to use a property name from the model as a path parameter?
## Answer
Depending on what you exactly want to do here, there is 2 options
```
import "@typespec/http";

using TypeSpec.Http;

// Option 1 - here you reuse the same property
@route("1/{countryCode}/update") op read1(...Country1): void;

model Country1 {
  @path countryCode: string;
  countryName: string;
}

// Option 2 - here the country is just the body and you still have an explicit path param 
@route("2/{countryCode}/update") op read2(
  @path countryCode: string,
  @body country: Country2,
): void;

model Country2 {
  countryCode: string;
  countryName: string;
}
```
[Playground example](https://typespec.io/playground?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7Cgp1c2luZyBUeXBlU3BlYy5IdHRwOwoKLy8gT3B0aW9uIDEKQHJvdXRlKCIxL3tjb3VudHJ5Q29kZX0vdXBkYXRlIikgb3AgcmVhZDEoLi4uQ8YiMSk6IHZvaWQ7Cgptb2RlbCDIGCB7CiAgQHBhdGggy006IHN0cmluZzsKIMgXTmFtyxd97ACTMukAkzL%2FAJMyKNxuLMQdYm9kecgdOugApzIsCvcAwTLlAMH%2FALvvALs%3D&e=%40typespec%2Fopenapi3&options=%7B%7D)

# How can I generate openapi json schema from typespec schema?

## Question  
I'm asking because the emit options does not seem to work for me. I'm trying:
```
npx tsp compile docs/main.tsp --emit="@typespec/json-schema"
```
and for example my main.tsp looks like:
```
import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi3";
import "@typespec/json-schema";

import "./Routes/auth.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.JsonSchema;

@service({
    title: "FCH API",
})
@server(
    "https://{url}/api/{apiVersion}",
    "Single server endpoint",
    {
        url: string = "127.0.0.1:8085",
        apiVersion: string = "v1",
    }
)
namespace FCH;
```
## Answer
Do you want to emit openapi3 or json schema? those are 2 different document(s) and emitters in TypeSpec

For openapi3 you should use the `@typespec/openapi3` emitter https://typespec.io/docs/libraries/openapi3/reference/emitter


# Introduce ::name meta property

## Question  
Given the following models:
```
@discriminator("_type")
model Piece<T extends {}> {
  @key 
  id: ID<T>; // custom scalar to denote an ID for a given model

  name: string;
  description: string;
}

model Course extends Piece<Course> {
  _type: "Course";
}

model Single extends Piece<Single> {
  _type: "Single";
}
```
And a newly introduced `::name` meta property, we could simplify it to:
```
@discriminator("_type")
model Piece<T extends {}> {
  _type: T::name;

  @key 
  id: string;

  name: string;
  description: string;
}

model Course extends Piece<Course> {}
model Single extends Piece<Single> {}
```

## Answer
I did file an issue for this recently, however there the goal i had was more to use in string interpolation. so would be good add your use case there to be sure it is considered in the design [#2964](https://github.com/microsoft/typespec/issues/2964)

# Adding HTTP status codes responses

## Question  
Hello,

I'm new to Typespec and I don't know how to assign different status codes in the response. Let's say I need to send a 409 response or any other specific HTTP status code.

Can you help me with that please?

Thanks!

## Answer
You can see examples [here](https://typespec.io/docs/getting-started/getting-started-http#status-codes). Essentially, when an operation returns a model, that model is checked for a field with the `@statusCode` decorator, which becomes the status code for that operation.

# How to append versions to URL?

## Question  
I am looking for a decorator to append versions to the URL.
My case is as below:
```
@versioned(Versions)
@route("petstore")
namespace PetStoreService;

enum Versions{
  v1,v2,v3
}

interface Pets extends Resource.ResourceOperations<Pet, Error> {}
```
There will generate three openapi files(v1,v2,v3) after executing `tsp complie .` and these files include the same interfaces below:
[POST] /petstore
[GET] /petstore
[GET] /petstore/{id}
[PATCH] /petstore/{id}
[DELETE] /petstore/{id}

My question is how to automatically append the version enum to the URL like:
```
// in openapi.v1.yaml like
[POST] /petstore/v1
[GET] /petstore/v1
[GET] /petstore/{id}/v1
[PATCH] /petstore/{id}/v1
[DELETE] /petstore/{id}/v1
```
```
// in openapi.v2.yaml like
[POST] /petstore/v2
[GET] /petstore/v2
[GET] /petstore/{id}/v2
[PATCH] /petstore/{id}/v2
[DELETE] /petstore/{id}/v2
...
```
```
// in openapi.v3.yaml like
[POST] /petstore/v3
[GET] /petstore/v3
[GET] /petstore/{id}/v3
[PATCH] /petstore/{id}/v3
[DELETE] /petstore/{id}/v3
...
```
## Answer
The short answer is that there isn't a way to automatically do this yet, however, you can achieve a similar thing by using explict `@route` decoration with versioned interfaces or operations, for example [this playground shows versioning interfaces](https://cadlplayground.z22.web.core.windows.net/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZyI7Cgp1c2luZyBUeXBlU3BlYy5IdHRwO9AVUmVzdNEVVslH1TDEBW91cmNlOwoKQHNlcnZpY2UoewogIHRpdGxlOiAiV2lkZ2V0IFPGGyIsCn0pCkDnAJxlZCjHX3MpCkByb3V0ZSgicGV0c3RvcmUiKQpuYW1lc3BhY2UgUGV0U8QVx0c7CgplbnVtIMg%2BIMRydjEsxAYyxQYzLAp9Cgptb2RlbMQ9xSFAa2V5CiAgxFY6IHN0cuUA1MU5UHJvcM0TQGFkZOsAni52MinFXN8pcy52M8UpM84pfQoKQGVycm9y5wCSRcQM5QCUY29k6wCNICBtZXNzYWfLE8Q7cmVtb3bNZjLqATAvdjEiKQpAcHJvamVjdGVkTmFtZSgianNvbiIsICJQZXRzQ29sbGVjdGlvbiIpCmludGVyZucBVMsbVjEgZXh0ZW5kcyDoAdPKHU9wZXJhxAlzPFBldCzmAMA%2BIHv%2FAJ0oInvkAW19%2FwCjZXRz8QCZVjHmAbNwYXRjaMQJdXBkYXRlc%2BgAnChQZXTkAWrGFyguLukCiVBhcmFtZXRlcuUArD4sIEBib2R5IMQFOsRbKcUGIHzmAMXmAWHzAe7zANgz7AF1Mv8A0v8BdcYbVjL%2FAXX4AXX%2FALHyALHoAYn%2FALfzAYky%2FwGJ%2FwGJ%2FwGJ%2FwGJ7QFzM%2F8AvP8Bc8YbdjP%2FAXP%2FAXP8AV3%2FAKHzAV0z%2FwFd%2FwFd%2FwFd7QFd&e=%40typespec%2Fopenapi3&options=%7B%7D)

Creating templates based on these definitions would make this a bit easier to express. In the future, it may be possible to use string interpolation to represent the version value in the operation route, which would remove the need to represent the versioned operatiosn separately.