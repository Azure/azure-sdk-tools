using System.Collections.Generic;
using swagger_api_parser;
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
        foreach (var child in node.Children)
        {
            this.output.WriteLine(child.CommonPath);
            foreach (var subchild in child.Children)
            {
                if (subchild.CommonPath.Length > 0)
                {
                    this.output.WriteLine($"  {subchild.CommonPath}");
                }
            }
        }
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
        this.output.WriteLine(node.CommonPath);
        
    }
    
}

