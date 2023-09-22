using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using SwaggerApiParser;
using SwaggerApiParser.SwaggerApiView;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class UtilsTest
{
    private readonly ITestOutputHelper output;

    public UtilsTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TestGetCommonPath()
    {
        var paths = new List<string>() {"/api/v1/users", "/api/v1/users/{id}", "/api/v1/users/{id}/friends"};
        var commonPath = Utils.GetCommonPath(paths);
        Assert.Equal("/api/v1/users", commonPath);

        paths = new List<string>() {"/deviceupdate/{instanceId}/management/deviceclasses", "/deviceupdate/{instanceId}/management/deviceclasses/{deviceClassId}/installableupdates"};

        commonPath = Utils.GetCommonPath(paths);
        Assert.Equal("/deviceupdate/{instanceId}/management/deviceclasses", commonPath);

        paths = new List<string>() {"/api/v1/get", "/demo"};
        commonPath = Utils.GetCommonPath(paths);
        Assert.Equal("", commonPath);
    }

    [Fact]
    public void TestBuildPathTreeSimpleCase()
    {
        var paths = new List<string>() {"/api/v1/users", "/api/v1/users/{id}", "/api/v1/users/{id}/friends"};
        var node = Utils.BuildPathTree(paths);
        this.output.WriteLine(node.CommonPath);
        this.output.WriteLine(Utils.VisualizePathTree(node));
    }

    [Fact]
    public void TestBuildPathTreeForDeviceUpdate()
    {
        var paths = new List<string>()
        {
            "/deviceupdate/{instanceId}/updates",
            "/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions/{version}",
            "/deviceupdate/{instanceId}/updates/providers",
            "/deviceupdate/{instanceId}/updates/providers/{provider}/names",
            "/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions",
            "/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions/{version}/files",
            "/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions/{version}/files/{fileId}",
            "/deviceupdate/{instanceId}/updates/operations",
            "/deviceupdate/{instanceId}/updates/operations/{operationId}",
            "/deviceupdate/{instanceId}/management/deviceclasses",
            "/deviceupdate/{instanceId}/management/deviceclasses/{deviceClassId}",
            "/deviceupdate/{instanceId}/management/deviceclasses/{deviceClassId}/installableupdates",
            "/deviceupdate/{instanceId}/management/devices",
            "/deviceupdate/{instanceId}/management/devices/{deviceId}",
            "/deviceupdate/{instanceId}/management/devices/{deviceId}/modules/{moduleId}",
            "/deviceupdate/{instanceId}/management/updatecompliance",
            "/deviceupdate/{instanceId}/management/devicetags",
            "/deviceupdate/{instanceId}/management/devicetags/{tagName}",
            "/deviceupdate/{instanceId}/management/groups",
            "/deviceupdate/{instanceId}/management/groups/{groupId}",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/updateCompliance",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/bestUpdates",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/deployments",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/deployments/{deploymentId}",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/deployments/{deploymentId}/status",
            "/deviceupdate/{instanceId}/management/groups/{groupId}/deployments/{deploymentId}/devicestates",
            "/deviceupdate/{instanceId}/management/operations/{operationId}",
            "/deviceupdate/{instanceId}/management/operations",
            "/deviceupdate/{instanceId}/management/deviceDiagnostics/logCollections/{operationId}",
            "/deviceupdate/{instanceId}/management/deviceDiagnostics/logCollections",
            "/deviceupdate/{instanceId}/management/deviceDiagnostics/logCollections/{operationId}/detailedStatus"
        };
        var node = Utils.BuildPathTree(paths);
        var firstLevelPath = node.Children.Select(child => child.CommonPath).ToList();

        Assert.Equal(firstLevelPath,
            new List<string>()
            {
                "/deviceupdate/{instanceId}/management/deviceclasses",
                "/deviceupdate/{instanceId}/management/deviceDiagnostics/logCollections",
                "/deviceupdate/{instanceId}/management/devices",
                "/deviceupdate/{instanceId}/management/devicetags",
                "/deviceupdate/{instanceId}/management/groups",
                "/deviceupdate/{instanceId}/management/operations",
                "/deviceupdate/{instanceId}"
            });
    }

    [Fact]
    public void TestBuildPathTreeForManagementCompute()
    {
        var paths = new List<string>()
        {
            "/providers/Microsoft.Compute/operations",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/availabilitySets/{availabilitySetName}",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/availabilitySets",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/availabilitySets",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/availabilitySets/{availabilitySetName}/vmSizes",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/proximityPlacementGroups/{proximityPlacementGroupName}",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/proximityPlacementGroups",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/proximityPlacementGroups",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups/{hostGroupName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/hostGroups",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups/{hostGroupName}/hosts/{hostName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups/{hostGroupName}/hosts",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/sshPublicKeys",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/sshPublicKeys",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/sshPublicKeys/{sshPublicKeyName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/sshPublicKeys/{sshPublicKeyName}/generateKeyPair",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmextension/types/{type}/versions/{version}",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmextension/types",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmextension/types/{type}/versions",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/extensions/{vmExtensionName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/extensions",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus/{skus}/versions/{version}",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus/{skus}/versions",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmimage/offers",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/edgeZones/{edgeZone}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus/{skus}/versions/{version}",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/edgeZones/{edgeZone}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus/{skus}/versions",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/edgeZones/{edgeZone}/publishers/{publisherName}/artifacttypes/vmimage/offers",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/edgeZones/{edgeZone}/publishers",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/edgeZones/{edgeZone}/publishers/{publisherName}/artifacttypes/vmimage/offers/{offer}/skus",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/usages",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/virtualMachines",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/virtualMachineScaleSets",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/vmSizes",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/images/{imageName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/images",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/images",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/capture",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/instanceView",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/convertToManagedDisks",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/deallocate",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/generalize",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachines",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/restorePointCollections/{restorePointCollectionName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/restorePointCollections",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/restorePointCollections",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/restorePointCollections/{restorePointCollectionName}/restorePoints/{restorePointName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/vmSizes",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/capacityReservationGroups/{capacityReservationGroupName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/capacityReservationGroups",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/capacityReservationGroups",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/capacityReservationGroups/{capacityReservationGroupName}/capacityReservations/{capacityReservationName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/capacityReservationGroups/{capacityReservationGroupName}/capacityReservations",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/powerOff",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/reapply",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/restart",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/start",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/redeploy",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/reimage",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/retrieveBootDiagnosticsData",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/performMaintenance",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/simulateEviction",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/assessPatches",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/installPatches",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/deallocate",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/delete",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/instanceView",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/extensions/{vmssExtensionName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/extensions",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachineScaleSets",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/skus",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/osUpgradeHistory",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/poweroff",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/restart",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/start",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/redeploy",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/performMaintenance",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/manualupgrade",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/reimage",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/reimageall",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/rollingUpgrades/cancel",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/osRollingUpgrade",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/extensionRollingUpgrade",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/rollingUpgrades/latest",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/forceRecoveryServiceFabricPlatformUpdateDomainWalk",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/convertToSinglePlacementGroup",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/setOrchestrationServiceState",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/extensions/{vmExtensionName}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/extensions",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/reimage",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/reimageall",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/deallocate",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/instanceView",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{virtualMachineScaleSetName}/virtualMachines",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/poweroff",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/restart",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/start",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/redeploy",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/retrieveBootDiagnosticsData",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualmachines/{instanceId}/performMaintenance",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmScaleSetName}/virtualMachines/{instanceId}/simulateEviction",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/logAnalytics/apiAccess/getRequestRateByInterval",
            "/subscriptions/{subscriptionId}/providers/Microsoft.Compute/locations/{location}/logAnalytics/apiAccess/getThrottledRequests",
            "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups/{hostGroupName}/hosts/{hostName}/restart"
        };
        var node = Utils.BuildPathTree(paths);
        var firstLevelPath = node.Children.Select(child => child.CommonPath).ToList();
        Assert.Equal(firstLevelPath,
            new List<string>()
            {
                "/providers/Microsoft.Compute/operations",
                "/subscriptions/{subscriptionId}/providers/Microsoft.Compute",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/availabilitySets",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/capacityReservationGroups",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/hostGroups",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/images",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/proximityPlacementGroups",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/restorePointCollections",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/sshPublicKeys",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines",
                "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets"
            });
    }

    [Fact]
    public void TestOperationIdPrefix()
    {
        var prefix = Utils.GetOperationIdPrefix("DedicatedHostGroups_CreateOrUpdate");
        Assert.Equal("DedicatedHostGroups", prefix);
    }

    [Fact]
    public void TestComputeOperationIdPrefix()
    {
        var operationIds = new List<string>()
        {
            "VirtualMachineRunCommands_List",
            "VirtualMachineRunCommands_Get",
            "VirtualMachines_RunCommand",
            "VirtualMachineScaleSetVMs_RunCommand",
            "VirtualMachineRunCommands_CreateOrUpdate",
            "VirtualMachineRunCommands_Update",
            "VirtualMachineRunCommands_Delete",
            "VirtualMachineRunCommands_GetByVirtualMachine",
            "VirtualMachineRunCommands_ListByVirtualMachine",
            "VirtualMachineScaleSetVMRunCommands_CreateOrUpdate",
            "VirtualMachineScaleSetVMRunCommands_Update",
            "VirtualMachineScaleSetVMRunCommands_Delete",
            "VirtualMachineScaleSetVMRunCommands_Get",
            "VirtualMachineScaleSetVMRunCommands_List",
            "Operations_List",
            "AvailabilitySets_CreateOrUpdate",
            "AvailabilitySets_Update",
            "AvailabilitySets_Delete",
            "AvailabilitySets_Get",
            "AvailabilitySets_ListBySubscription",
            "AvailabilitySets_List",
            "AvailabilitySets_ListAvailableSizes",
            "ProximityPlacementGroups_CreateOrUpdate",
            "ProximityPlacementGroups_Update",
            "ProximityPlacementGroups_Delete",
            "ProximityPlacementGroups_Get",
            "ProximityPlacementGroups_ListBySubscription",
            "ProximityPlacementGroups_ListByResourceGroup",
            "DedicatedHostGroups_CreateOrUpdate",
            "DedicatedHostGroups_Update",
            "DedicatedHostGroups_Delete",
            "DedicatedHostGroups_Get",
            "DedicatedHostGroups_ListByResourceGroup",
            "DedicatedHostGroups_ListBySubscription",
            "DedicatedHosts_CreateOrUpdate",
            "DedicatedHosts_Update",
            "DedicatedHosts_Delete",
            "DedicatedHosts_Get",
            "DedicatedHosts_ListByHostGroup",
            "SshPublicKeys_ListBySubscription",
            "SshPublicKeys_ListByResourceGroup",
            "SshPublicKeys_Create",
            "SshPublicKeys_Update",
            "SshPublicKeys_Delete",
            "SshPublicKeys_Get",
            "SshPublicKeys_GenerateKeyPair",
            "VirtualMachineExtensionImages_Get",
            "VirtualMachineExtensionImages_ListTypes",
            "VirtualMachineExtensionImages_ListVersions",
            "VirtualMachineExtensions_CreateOrUpdate",
            "VirtualMachineExtensions_Update",
            "VirtualMachineExtensions_Delete",
            "VirtualMachineExtensions_Get",
            "VirtualMachineExtensions_List",
            "VirtualMachineImages_Get",
            "VirtualMachineImages_List",
            "VirtualMachineImages_ListOffers",
            "VirtualMachineImages_ListPublishers",
            "VirtualMachineImages_ListSkus",
            "VirtualMachineImagesEdgeZone_Get",
            "VirtualMachineImagesEdgeZone_List",
            "VirtualMachineImagesEdgeZone_ListOffers",
            "VirtualMachineImagesEdgeZone_ListPublishers",
            "VirtualMachineImagesEdgeZone_ListSkus",
            "Usage_List",
            "VirtualMachines_ListByLocation",
            "VirtualMachineScaleSets_ListByLocation",
            "VirtualMachineSizes_List",
            "Images_CreateOrUpdate",
            "Images_Update",
            "Images_Delete",
            "Images_Get",
            "Images_ListByResourceGroup",
            "Images_List",
            "VirtualMachines_Capture",
            "VirtualMachines_CreateOrUpdate",
            "VirtualMachines_Update",
            "VirtualMachines_Delete",
            "VirtualMachines_Get",
            "VirtualMachines_InstanceView",
            "VirtualMachines_ConvertToManagedDisks",
            "VirtualMachines_Deallocate",
            "VirtualMachines_Generalize",
            "VirtualMachines_List",
            "VirtualMachines_ListAll",
            "RestorePointCollections_CreateOrUpdate",
            "RestorePointCollections_Update",
            "RestorePointCollections_Delete",
            "RestorePointCollections_Get",
            "RestorePointCollections_List",
            "RestorePointCollections_ListAll",
            "RestorePoints_Create",
            "RestorePoints_Delete",
            "RestorePoints_Get",
            "VirtualMachines_ListAvailableSizes",
            "CapacityReservationGroups_CreateOrUpdate",
            "CapacityReservationGroups_Update",
            "CapacityReservationGroups_Delete",
            "CapacityReservationGroups_Get",
            "CapacityReservationGroups_ListByResourceGroup",
            "CapacityReservationGroups_ListBySubscription",
            "CapacityReservations_CreateOrUpdate",
            "CapacityReservations_Update",
            "CapacityReservations_Delete",
            "CapacityReservations_Get",
            "CapacityReservations_ListByCapacityReservationGroup",
            "VirtualMachines_PowerOff",
            "VirtualMachines_Reapply",
            "VirtualMachines_Restart",
            "VirtualMachines_Start",
            "VirtualMachines_Redeploy",
            "VirtualMachines_Reimage",
            "VirtualMachines_RetrieveBootDiagnosticsData",
            "VirtualMachines_PerformMaintenance",
            "VirtualMachines_SimulateEviction",
            "VirtualMachines_AssessPatches",
            "VirtualMachines_InstallPatches",
            "VirtualMachineScaleSets_CreateOrUpdate",
            "VirtualMachineScaleSets_Update",
            "VirtualMachineScaleSets_Delete",
            "VirtualMachineScaleSets_Get",
            "VirtualMachineScaleSets_Deallocate",
            "VirtualMachineScaleSets_DeleteInstances",
            "VirtualMachineScaleSets_GetInstanceView",
            "VirtualMachineScaleSets_List",
            "VirtualMachineScaleSetExtensions_CreateOrUpdate",
            "VirtualMachineScaleSetExtensions_Update",
            "VirtualMachineScaleSetExtensions_Delete",
            "VirtualMachineScaleSetExtensions_Get",
            "VirtualMachineScaleSetExtensions_List",
            "VirtualMachineScaleSets_ListAll",
            "VirtualMachineScaleSets_ListSkus",
            "VirtualMachineScaleSets_GetOSUpgradeHistory",
            "VirtualMachineScaleSets_PowerOff",
            "VirtualMachineScaleSets_Restart",
            "VirtualMachineScaleSets_Start",
            "VirtualMachineScaleSets_Redeploy",
            "VirtualMachineScaleSets_PerformMaintenance",
            "VirtualMachineScaleSets_UpdateInstances",
            "VirtualMachineScaleSets_Reimage",
            "VirtualMachineScaleSets_ReimageAll",
            "VirtualMachineScaleSetRollingUpgrades_Cancel",
            "VirtualMachineScaleSetRollingUpgrades_StartOSUpgrade",
            "VirtualMachineScaleSetRollingUpgrades_StartExtensionUpgrade",
            "VirtualMachineScaleSetRollingUpgrades_GetLatest",
            "VirtualMachineScaleSets_ForceRecoveryServiceFabricPlatformUpdateDomainWalk",
            "VirtualMachineScaleSets_ConvertToSinglePlacementGroup",
            "VirtualMachineScaleSets_SetOrchestrationServiceState",
            "VirtualMachineScaleSetVMExtensions_CreateOrUpdate",
            "VirtualMachineScaleSetVMExtensions_Update",
            "VirtualMachineScaleSetVMExtensions_Delete",
            "VirtualMachineScaleSetVMExtensions_Get",
            "VirtualMachineScaleSetVMExtensions_List",
            "VirtualMachineScaleSetVMs_Reimage",
            "VirtualMachineScaleSetVMs_ReimageAll",
            "VirtualMachineScaleSetVMs_Deallocate",
            "VirtualMachineScaleSetVMs_Update",
            "VirtualMachineScaleSetVMs_Delete",
            "VirtualMachineScaleSetVMs_Get",
            "VirtualMachineScaleSetVMs_GetInstanceView",
            "VirtualMachineScaleSetVMs_List",
            "VirtualMachineScaleSetVMs_PowerOff",
            "VirtualMachineScaleSetVMs_Restart",
            "VirtualMachineScaleSetVMs_Start",
            "VirtualMachineScaleSetVMs_Redeploy",
            "VirtualMachineScaleSetVMs_RetrieveBootDiagnosticsData",
            "VirtualMachineScaleSetVMs_PerformMaintenance",
            "VirtualMachineScaleSetVMs_SimulateEviction",
            "LogAnalytics_ExportRequestRateByInterval",
            "LogAnalytics_ExportThrottledRequests",
            "DedicatedHosts_Restart"
        };

        var operationIdPrefix = operationIds.Select(Utils.GetOperationIdPrefix);
        foreach (var prefix in operationIdPrefix)
        {
            this.output.WriteLine(prefix);
        }
    }
}
