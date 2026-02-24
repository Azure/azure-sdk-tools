
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