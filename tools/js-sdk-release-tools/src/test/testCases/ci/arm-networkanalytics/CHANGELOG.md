# Release History
    
## 2.0.0 (2024-06-17)
    
**Features**

  - Added operation DataProductsOperations.create
  - Added operation DataProductsOperations.delete
  - Added operation DataProductsOperations.update
  - Added operation DataTypesOperations.create
  - Added operation DataTypesOperations.delete
  - Added operation DataTypesOperations.deleteData
  - Added operation DataTypesOperations.update
  - Added Interface ArmOperationStatus
  - Added Interface NetworkAnalyticsClientOptions
  - Added Interface PagedAsyncIterableIterator
  - Added Interface PagedOperation
  - Added Interface PageSettings
  - Added Interface RestorePollerOptions
  - Added Class NetworkAnalyticsClient
  - Added Type Alias ContinuablePage
  - Added Type Alias ResourceProvisioningState
  - Added function restorePoller

**Breaking Changes**

  - Removed operation DataProducts.create
  - Removed operation DataProducts.delete
  - Removed operation DataProducts.update
  - Removed operation DataTypes.create
  - Removed operation DataTypes.delete
  - Removed operation DataTypes.deleteData
  - Removed operation DataTypes.update
  - Deleted Class MicrosoftNetworkAnalytics
  - Interface DataProductsCreateOptionalParams no longer has parameter resumeFrom
  - Interface DataProductsDeleteOptionalParams no longer has parameter resumeFrom
  - Interface DataProductsUpdateOptionalParams no longer has parameter resumeFrom
  - Interface DataTypesCreateOptionalParams no longer has parameter resumeFrom
  - Interface DataTypesDeleteDataOptionalParams no longer has parameter resumeFrom
  - Interface DataTypesDeleteOptionalParams no longer has parameter resumeFrom
  - Interface DataTypesUpdateOptionalParams no longer has parameter resumeFrom
  - Type of parameter tags of interface DataProductUpdate is changed from {
        [propertyName: string]: string;
    } to Record<string, string>
  - Type of parameter info of interface ErrorAdditionalInfo is changed from Record<string, unknown> to Record<string, any>
  - Type of parameter userAssignedIdentities of interface ManagedServiceIdentity is changed from {
        [propertyName: string]: UserAssignedIdentity;
    } to Record<string, UserAssignedIdentity>
  - Type of parameter tags of interface TrackedResource is changed from {
        [propertyName: string]: string;
    } to Record<string, string>
  - Removed Enum KnownActionType
  - Removed Enum KnownBypass
  - Removed Enum KnownControlState
  - Removed Enum KnownCreatedByType
  - Removed Enum KnownDataProductUserRole
  - Removed Enum KnownDataTypeState
  - Removed Enum KnownDefaultAction
  - Removed Enum KnownManagedServiceIdentityType
  - Removed Enum KnownOrigin
  - Removed Enum KnownProvisioningState
  - Removed Enum KnownVersions
  - Removed function getContinuationToken
    
    
## 1.0.0 (2024-01-24)

The package of @azure/arm-networkanalytics is using our next generation design principles. To learn more, please refer to our documentation [Quick Start](https://aka.ms/azsdk/js/mgmt/quickstart).
