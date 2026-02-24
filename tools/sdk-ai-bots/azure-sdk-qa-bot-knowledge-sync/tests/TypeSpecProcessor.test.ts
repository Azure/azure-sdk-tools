import { describe, it, expect, beforeEach } from 'vitest';
import { TypeSpecProcessor } from '../src/services/TypeSpecProcessor';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

describe('TypeSpecProcessor', () => {
    let processor: TypeSpecProcessor;
    let testDir: string;

    beforeEach(() => {
        processor = new TypeSpecProcessor(__dirname, "testData/TypeSpecProcessor");
    });

    describe('parseTypeSpecDefinitions', () => {
        it('should parse a simple model definition', () => {
            const content = `
model User {
    name: string;
    age: int32;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(1);
            expect(definitions[0].type).toBe('model');
            expect(definitions[0].name).toBe('User');
            expect(definitions[0].code).toContain('name: string');
        });

        it('should parse multiple definitions', () => {
            const content = `
model User {
    name: string;
}

enum Status {
    Active,
    Inactive
}

op getUser(): User;
`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(3);
            expect(definitions[0].type).toBe('model');
            expect(definitions[0].name).toBe('User');
            expect(definitions[1].type).toBe('enum');
            expect(definitions[1].name).toBe('Status');
            expect(definitions[2].type).toBe('operation');
            expect(definitions[2].name).toBe('getUser');
        });

        it('should extract description from @doc decorator', () => {
            const content = `
@doc("A user in the system")
model User {
    name: string;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions[0].description).toBe('A user in the system');
        });

        it('should extract description from JSDoc comments', () => {
            const content = `
/**
 * A user in the system
 */
model User {
    name: string;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions[0].description).toBe('A user in the system');
        });

        it('should extract description from single-line comments', () => {
            const content = `
// A user in the system
model User {
    name: string;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions[0].description).toBe('A user in the system');
        });

        it('should capture decorators', () => {
            const content = `
@doc("A user model")
@resource("users")
model User {
    name: string;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions[0].decorators).toHaveLength(2);
            expect(definitions[0].decorators[0]).toContain('@doc');
            expect(definitions[0].decorators[1]).toContain('@resource');
        });

        it('should parse interface definitions', () => {
            const content = `
interface UserOperations {
    getUser(): User;
    createUser(user: User): User;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(1);
            expect(definitions[0].type).toBe('interface');
            expect(definitions[0].name).toBe('UserOperations');
        });

        it('should parse interface template definitions', () => {
            const content = `
/**
 * This is the interface which contains different types of operation definition.
 * all supported RP operations. You should have exactly one declaration for each
 * Azure Resource Manager service. It implements
 *   GET "/providers/{provider-namespace}/operations"
 *
 */
interface UserOperations {
    getUser(): User;
    createUser(user: User): User;
    get is ArmResourceRead<User>;
    op list(): User[];
}
/**
 * An operation template used to build resource operations in which the same resource type
 * is accessible at multiple, fixed resource paths. Can be used with static routes.
 * @template ParentParameters The path parameters for the resource parent
 * @template ResourceTypeParameter The path parameter for the resource name
 * @template ErrorType Optional. The type of error models used in operations created form this template
 * @template ResourceRoute Optional. The resource route to use for operations in the interface.
 * @template RoutedResourceName Optional. The name of the resource type described in this template
 */
@doc("")
interface RoutedOperations<
  ParentParameters extends {},
  ResourceTypeParameter extends {},
  ErrorType extends {} = ErrorResponse,
  ResourceRoute extends valueof ArmOperationOptions = #{ useStaticRoute: false },
  RoutedResourceName extends valueof string = string("")
> {
  /**
   * A long-running resource CreateOrUpdate (PUT)
   * @template Resource the resource being created or updated
   * @template LroHeaders Optional.  Allows overriding the lro headers returned on resource create
   * @template Parameters Optional. Additional parameters after the path parameters
   * @template Response Optional. The success response(s) for the PUT operation
   * @template OptionalRequestBody Optional. Indicates whether the request body is optional
   * @template OverrideErrorType Optional. The error response, if non-standard.
   * @template OverrideRouteOptions Optional. The route options for the operation.
   * @template Request Optional. The request body for the createOrUpdate operation.
   * @template OverrideResourceName Optional. The name of the resource type being acted upon.
   */
  @doc("Create a {name}", Resource)
  @armOperationRoute(OverrideRouteOptions)
  @legacyResourceOperation(Resource, "createOrUpdate", OverrideResourceName)
  @Private.armUpdateProviderNamespace
  @Azure.Core.Foundations.Private.defaultFinalStateVia(#["location", "azure-async-operation"])
  @put
  CreateOrUpdateAsync<
    Resource extends Foundations.SimpleResource,
    LroHeaders extends TypeSpec.Reflection.Model = ArmAsyncOperationHeader<FinalResult = Resource> &
      Azure.Core.Foundations.RetryAfterHeader,
    Parameters extends {} = {},
    Response extends {} = ArmResourceUpdatedResponse<Resource> | ArmResourceCreatedResponse<
      Resource,
      LroHeaders
    >,
    OptionalRequestBody extends valueof boolean = false,
    OverrideErrorType extends {} = ErrorType,
    OverrideRouteOptions extends valueof ArmOperationOptions = ResourceRoute,
    Request extends {} | void = Resource,
    OverrideResourceName extends valueof string = "RoutedResourceName"
  >(
    ...ParentParameters,
    ...ResourceTypeParameter,
    ...Parameters,
    @doc("Resource create parameters.") @armBodyRoot(OptionalRequestBody) resource: Request,
  ): Response | OverrideErrorType;
`;
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(2);
            expect(definitions[0].type).toBe('interface');
            expect(definitions[0].name).toBe('UserOperations');
            expect(definitions[0].children.length).toBe(4);
            expect(definitions[1].type).toBe('interface');
            expect(definitions[1].name).toBe('RoutedOperations');
            expect(definitions[1].children.length).toBe(1);
            expect(definitions[1].children[0].name).toBe('CreateOrUpdateAsync');
        });

        it('should parse union definitions', () => {
            const content = `
union StringOrNumber {
    string,
    number
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(1);
            expect(definitions[0].type).toBe('union');
            expect(definitions[0].name).toBe('StringOrNumber');
        });

        it('should parse alias definitions', () => {
            const processor = new TypeSpecProcessor("D:\\project", "lib");
            const content = `
alias UserId = string;
`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(1);
            expect(definitions[0].type).toBe('alias');
            expect(definitions[0].name).toBe('UserId');
        });

        it('should parse blockless namespace', () => {
            const content = `
namespace MyService.Models;
model User {
    name: string;
}
`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            expect(definitions.length === 2);
            expect(definitions[0].type).toBe('namespace');
            expect(definitions[0].name).toBe('MyService.Models');
            expect(definitions[1].type).toBe('model');
            expect(definitions[1].name).toBe('User');
        });

        it('should parse block-style namespace definitions', () => {
            const content = `
namespace MyService.Models {
    model User {
        name: string;
    }
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            expect(definitions.length === 1);
            expect(definitions[0].type).toBe('namespace');
            expect(definitions[0].name).toBe('MyService.Models');
            expect(definitions[0].children.length === 1);
            expect(definitions[0].children[0].name).toBe('User');
            expect(definitions[0].children[0].type).toBe('model');
        });

        it('should parse scalar definitions', () => {
            const content = `
scalar uuid extends string;
`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(1);
            expect(definitions[0].type).toBe('scalar');
            expect(definitions[0].name).toBe('uuid');
        });

        it('should handle multi-line @doc decorator', () => {
            const content = `
@doc("""
This is a multi-line
documentation string
""")
model User {
    name: string;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions[0].description).toContain('multi-line');
        });

        it('operation template', () => {
            const processor = new TypeSpecProcessor("D:\\project", "lib");
            const content = `
/**
 * A resource list operation, at the subscription scope
 * @template Resource the resource being patched
 * @template Parameters Optional. Additional parameters after the path parameters
 * @template Response Optional. The success response for the list operation
 * @template Error Optional. The error response, if non-standard.
 */
@autoRoute
@doc("List {name} resources by subscription ID", Resource)
@list
@listsResource(Resource)
@segmentOf(Resource)
@armResourceList(Resource)
@get
@Private.enforceConstraint(Resource, Foundations.Resource)
op ArmListBySubscription<
  Resource extends Foundations.SimpleResource,
  Parameters extends {} = {},
  Response extends {} = ArmResponse<ResourceListResult<Resource>>,
  Error extends {} = ErrorResponse
> is ArmReadOperation<SubscriptionScope<Resource> & Parameters, Response, Error>;

/**
 * A resource list operation, at the scope of the resource's parent
 * @template Resource the resource being patched
 * @template BaseParameters Optional. Allows overriding the operation parameters
 * @template ParentName Optional. The name of the parent resource
 * @template ParentFriendlyName Optional. The friendly name of the parent resource
 * @template Parameters Optional. Additional parameters after the path parameters
 * @template Response Optional. The success response for the list operation
 * @template Error Optional. The error response, if non-standard.
 */
@get
@autoRoute
@list
@listsResource(Resource)
@segmentOf(Resource)
@Private.armRenameListByOperation(Resource, ParentName, ParentFriendlyName, false) // This must come before @armResourceList!
@armResourceList(Resource)
@Private.enforceConstraint(Resource, Foundations.Resource)
op ArmResourceListByParent<
  Resource extends Foundations.SimpleResource,
  BaseParameters = DefaultBaseParameters<Resource>,
  ParentName extends valueof string = "",
  ParentFriendlyName extends valueof string = "",
  Parameters extends {} = {},
  Response extends {} = ArmResponse<ResourceListResult<Resource>>,
  Error extends {} = ErrorResponse
> is ArmReadOperation<
  ResourceParentParameters<Resource, BaseParameters> & Parameters,
  Response,
  Error
>;
`;
            const definitions = processor['parseTypeSpecDefinitions'](content);
            expect(definitions).toHaveLength(2);
            expect(definitions[0].type).toBe('operation');
            expect(definitions[0].name).toBe('ArmListBySubscription');
            expect(definitions[0].description).toContain('A resource list operation, at the subscription scope');
        });
    });

    describe('processTypeSpecLibraries', () => {
        it('convert all typespec files to markdown', () => {
            processor.processTypeSpecLibraries();
            const testDataDir = path.join(__dirname, "testData", "TypeSpecProcessor");
            const generatedDir = path.join(testDataDir, "generated");
            expect(fs.existsSync(generatedDir)).toBe(true);
            
            // Find all .tsp files in testData folder
            const tspFiles = fs.readdirSync(testDataDir).filter(f => f.endsWith('.tsp'));
            
            for (const tspFile of tspFiles) {
                const baseName = tspFile.replace('.tsp', '');
                const generatedFile = path.join(generatedDir, `${baseName}.md`);
                const expectedFile = path.join(testDataDir, `expected_${baseName}.md`);
                
                // Skip if no expected file exists
                if (!fs.existsSync(expectedFile)) {
                    console.log(`Skipping ${tspFile}: no expected file found`);
                    continue;
                }
                
                expect(fs.existsSync(generatedFile), `Generated file not found: ${generatedFile}`).toBe(true);
                
                const generated = fs.readFileSync(generatedFile, 'utf-8');
                const expected = fs.readFileSync(expectedFile, 'utf-8');
                expect(generated).toBe(expected);
            }
        });
    });
});
