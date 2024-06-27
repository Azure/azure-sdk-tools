import unittest
from os import path

from build import DotNetBuild
from models import DotNetExample


class TestDotNetBuild(unittest.TestCase):

    def test_example(self):
        code = '''using System;
using System.Threading.Tasks;
using System.Xml;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Compute;

// authenticate your client
ArmClient client = new ArmClient(new DefaultAzureCredential());

// this example assumes you already have this VirtualMachineResource created on azure
// for more information of creating VirtualMachineResource, please refer to the document of VirtualMachineResource
string subscriptionId = "{subscription-id}";
string resourceGroupName = "myResourceGroup";
string vmName = "myVM";
ResourceIdentifier virtualMachineResourceId = VirtualMachineResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vmName);
VirtualMachineResource virtualMachine = client.GetVirtualMachineResource(virtualMachineResourceId);

// invoke the operation
VirtualMachineResource result = await virtualMachine.GetAsync();

// the variable result is a resource, you could call other operations on this instance as well
// but just for demo, we get its data from this resource instance
VirtualMachineData resourceData = result.Data;
// for demo we just print out the id
Console.WriteLine($"Succeeded on id: {resourceData.Id}");
'''

        tmp_path = path.abspath('.')
        dotnet_examples = [DotNetExample('code', '', code)]
        dotnet_build = DotNetBuild(tmp_path, 'Azure.ResourceManager.Compute', '1.0.1', dotnet_examples)
        result = dotnet_build.build()
        self.assertTrue(result.succeeded)

    def test_invalid(self):
        code = '''using System;
using System.Threading.Tasks;
using System.Xml;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Compute;

ArmClient client = new ArmClient(new DefaultAzureCredential());

string resourceGroupName = "myResourceGroup";
string vmName = "myVM";
ResourceIdentifier virtualMachineResourceId = VirtualMachineResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vmName);
VirtualMachineResource virtualMachine = client.GetVirtualMachineResource(virtualMachineResourceId);
'''

        tmp_path = path.abspath('.')
        dotnet_examples = [DotNetExample('code', '', code)]
        dotnet_build = DotNetBuild(tmp_path, 'Azure.ResourceManager.Compute', '1.0.1', dotnet_examples)
        result = dotnet_build.build()
        self.assertFalse(result.succeeded)
