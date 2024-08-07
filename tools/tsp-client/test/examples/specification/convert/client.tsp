import "./main.tsp";
import "@azure-tools/typespec-client-generator-core";

using Azure.ClientGenerator.Core;
using AzureSphereManagementClient;

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Catalog.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Certificate.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Image.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Deployment.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(DeviceGroup.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Device.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(Product.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(ProductUpdate.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(DeviceGroupUpdate.properties);

#suppress "deprecated" "@flattenProperty decorator is not recommended to use."
@@flattenProperty(DeviceUpdate.properties);
