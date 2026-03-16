
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
    OverrideResourceName extends valueof string = "${RoutedResourceName}"
  >(
    ...ParentParameters,
    ...ResourceTypeParameter,
    ...Parameters,
    @doc("Resource create parameters.") @armBodyRoot(OptionalRequestBody) resource: Request,
  ): Response | OverrideErrorType;
}