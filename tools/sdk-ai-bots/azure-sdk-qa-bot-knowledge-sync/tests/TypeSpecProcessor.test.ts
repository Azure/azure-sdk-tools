import { describe, it, expect, beforeEach } from 'vitest';
import { TypeSpecProcessor } from '../src/services/TypeSpecProcessor';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

describe('TypeSpecProcessor', () => {
    let processor: TypeSpecProcessor;
    let testDir: string;

    beforeEach(() => {
        processor = new TypeSpecProcessor(__dirname, "testData");
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

        it('should parse interface definitions and extract operations', () => {
            const content = `
interface UserOperations {
    getUser(): User;
    createUser(user: User): User;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            // Interface should be parsed into individual operations
            expect(definitions).toHaveLength(2);
            expect(definitions[0].type).toBe('operation');
            expect(definitions[0].name).toBe('getUser');
            expect(definitions[1].type).toBe('operation');
            expect(definitions[1].name).toBe('createUser');
        });

        it('should extract decorators and comments from interface operations', () => {
            const content = `
interface UserOperations {
    /**
     * Get a user by ID
     */
    @get
    @route("users/{id}")
    getUser(@path id: string): User;
    
    @post
    @route("users")
    createUser(@body user: User): User;
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);
            
            expect(definitions).toHaveLength(2);
            
            // First operation should have comments and decorators
            expect(definitions[0].name).toBe('getUser');
            expect(definitions[0].description).toContain('Get a user by ID');
            expect(definitions[0].decorators).toHaveLength(2);
            expect(definitions[0].decorators[0]).toContain('@get');
            expect(definitions[0].decorators[1]).toContain('@route');
            
            // Second operation should have decorators
            expect(definitions[1].name).toBe('createUser');
            expect(definitions[1].decorators).toHaveLength(2);
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

        it('should parse namespace definitions', () => {
            const content = `
namespace MyService.Models {
    model User {
        name: string;
    }
}`;
            
            const definitions = processor['parseTypeSpecDefinitions'](content);

            expect(definitions[0].type).toBe('namespace');
            expect(definitions[0].name).toBe('MyService.Models');
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
        it('convert typespec operations to markdown', () => {
            processor.processTypeSpecLibraries();
            const generatedDir = path.join(__dirname, "testData", "generated");
            expect(fs.existsSync(generatedDir));
            const generatedFile = path.join(generatedDir, "operations.md");
            const generated = fs.readFileSync(generatedFile, 'utf-8');
            const expected = fs.readFileSync(path.join(__dirname, "testData", "expected_operations.md"), 'utf-8');
            expect(generated).toBe(expected);
        });
    });
});
