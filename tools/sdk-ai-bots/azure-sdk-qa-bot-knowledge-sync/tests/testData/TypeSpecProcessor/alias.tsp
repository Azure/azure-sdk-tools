namespace Azure.ResourceManager;

using Azure.ResourceManager.Private;

/**
 * Alias for network security perimeter configuration operations.
 *
 * @template NspConfigurationResourceModel The type of the network security perimeter configuration resource. You must declare a network security perimeter configuration resource type in your provider namespace.
 * @template NspConfigurationKeyName Optional. The NSP configuration resource name parameter. By default, this is `networkSecurityPerimeterConfigurationName`.
 * @template ResourceName Optional. The name of the network security perimeter configuration resource in this context, by default, this is `{targetResourceName}NetworkSecurityPerimeterConfiguration`.
 * @template ResourceParameter Optional. The name parameter for this NSP. By default, this is `NetworkSecurityPerimeterConfigurationNameParameter`.
 * @example
 * ```ts
 * namespace Microsoft.Contoso;
 * model NspConfiguration is NspConfigurationResource {}
 * alias EmployeeNspConfigurationOps is NspConfigurations<NspConfiguration>;
 * @armResourceOperations
 * interface Employees {
 *  @doc("get a network security perimeter configuration for resource employee")
 *  getNspConfiguration is EmployeeNspConfigurationOps.Read<Employee>;
 * }
 * ```
 */
alias NspConfigurations<
  NspConfigurationResourceModel extends CommonTypes.NetworkSecurityPerimeterConfiguration,
  NspConfigurationKeyName extends valueof string = "networkSecurityPerimeterConfigurationName",
  ResourceName extends valueof string = string(""),
  ResourceParameter extends {} = CommonTypes.NetworkSecurityPerimeterConfigurationNameParameter
> = CommonTypes.NspConfigurationOperations<
  NspConfigurationResourceModel,
  NspConfigurationKeyName,
  ResourceName,
  ResourceParameter
>;

/** Network security perimeter (NSP) configuration resource. Providers must instantiate an instance of this resource in their
 * provider namespace to enable NSP capabilities for their resources.
 *
 * @template KeyName Optional. The name of the NSP configuration resource name parameter. By default, this is `networkSecurityPerimeterConfigurationName`.
 * @template Description Optional. The documentary description of the NSP configuration resource name parameter.
 *
 * @example
 * ```ts
 * namespace Microsoft.Contoso;
 * model NspConfiguration is NspConfigurationResource {}
 * alias EmployeeConfigOps is NspConfigurations<NspConfiguration>;
 * @armResourceOperations
 * interface Employees {
 *  @doc("get a network security perimeter configuration for resource employee")
 *  getNspConfiguration is EmployeeConfigOps.Read<Employee>;
 * }
 * ```
 */
alias NspConfiguration<
  KeyName extends valueof string = "networkSecurityPerimeterConfigurationName",
  Description extends valueof string = "A network security perimeter configuration resource."
> = CommonTypes.NspConfigurationResource<KeyName, Description>;
