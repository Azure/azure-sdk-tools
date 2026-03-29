using Http;
using Rest;
using Azure.ResourceManager.Foundations;
using Azure.ResourceManager.Private;

namespace Azure.ResourceManager;

namespace subNameSpace {
    model Employee {
        name: string;
    }
}

model manager {
    id: string;
}

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