import unittest
from main import get_js_example_method, get_sample_version, get_module_relative_path, \
    break_down_aggregated_js_example, format_js, create_js_examples, Release


class TestMain(unittest.TestCase):

    def test_get_sample_version(self):
        self.assertEqual('v3', get_sample_version('3.0.0'))
        self.assertEqual('v3-beta', get_sample_version('3.0.0-beta.3'))

    def test_get_js_example_method(self):
        code = '''const { StorSimpleManagementClient } = require("@azure/arm-storsimple1200series");
const { DefaultAzureCredential } = require("@azure/identity");

/**
 * This sample demonstrates how to Upload Vault Cred Certificate.
Returns UploadCertificateResponse
 *
 * @summary Upload Vault Cred Certificate.
Returns UploadCertificateResponse
 * x-ms-original-file: specification/storSimple1200Series/resource-manager/Microsoft.StorSimple/stable/2016-10-01/examples/ManagersUploadRegistrationCertificate.json
 */
async function managersUploadRegistrationCertificate() {
  const subscriptionId = "4385cf00-2d3a-425a-832f-f4285b1c9dce";
  const certificateName = "windows";
  const resourceGroupName = "ResourceGroupForSDKTest";
  const managerName = "ManagerForSDKTest2";
  const uploadCertificateRequestrequest = {
    authType: "AzureActiveDirectory",
    certificate:
      "MIIC3TCCAcWgAwIBAgIQEr0bAWD6wJtA4LIbZ9NtgzANBgkqhkiG9w0BAQUFADAeMRwwGgYDVQQDExNXaW5kb3dzIEF6dXJlIFRvb2xzMB4XDTE4MDkxMDE1MzY0MFoXDTE4MDkxMzE1NDY0MFowHjEcMBoGA1UEAxMTV2luZG93cyBBenVyZSBUb29sczCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBANUsKkz2Z4fECKMyNeLb9v3pr1XF4dVe+MITDtgphjl81ng190Y0IHgCVnh4YjfplUSpMk/1xii0rI5AAPwoz3ze2qRPtnwCiTaoVLkUe6knNRPzrRvVXHB81J0/14MO0lwdByHhdccRcVJZPLt5724t4aQny82v2AayJdDDkBBWNlpcqPy6n3sygP00THMPP0O3sFqy924eHqoDj3qSw79/meaZBJt9S5odPuFoskxjHuI4lM6BmK1Ql7p8Wo9/GhTOIoMz81orKPHRDleLjutwL4mb6NnhI5rfT/MxnHD6m82c4YYqiZC3XiTyJWVCkWkp7PK92OdRp6FA87rdKDMCAwEAAaMXMBUwEwYDVR0lBAwwCgYIKwYBBQUHAwIwDQYJKoZIhvcNAQEFBQADggEBAIYlezVU68TuEblkn06vM5dfzSmHKJOQgW61nDlLnyKrmSJtzKZLCAswTE2VyJHwKNdZgW15coJFINjWBLWcLr0/GjNV4u3Z+UL3NhBFQd5xuMtKsIhuoscKtyk0JHQXpBvHNmOUCobfQfOBQfTVC7kmyWdtlGztFUVxD28s6S5gMb1FEWWN68NOOJ3/ZhaTbUEM54yw8Hk8/f0L/Zn/7BYHUyWWA3KStAaYn89C/ZFF+952ark2VaKGIjBRQzgrJEIR8dI4r46I3DoEfzGPESKvQPvVLhOX84RG0PLPOtnRbHBVew1Nh3HE9kgCubkPKK+NPWE9IHZPoRmOTWBe+zU=",
    contractVersion: "V2012_12",
  };
  const credential = new DefaultAzureCredential();
  const client = new StorSimpleManagementClient(credential, subscriptionId);
  const result = await client.managers.uploadRegistrationCertificate(
    certificateName,
    resourceGroupName,
    managerName,
    uploadCertificateRequestrequest
  );
  console.log(result);
}

managersUploadRegistrationCertificate().catch(console.error);
'''

        lines = code.splitlines(keepends=True)

        js_example_method = get_js_example_method(lines, 0, False)
        self.assertEqual(3, js_example_method.line_start)
        self.assertIsNotNone(js_example_method.line_end)

    def test_break_down_aggregated_js_example(self):
        code = '''const { StorageManagementClient } = require("@azure/arm-storage");
const { DefaultAzureCredential } = require("@azure/identity");

/**
 * This sample demonstrates how to Gets properties of a specified container.
 *
 * @summary Gets properties of a specified container.
 * x-ms-original-file: specification/storage/resource-manager/Microsoft.Storage/stable/2022-09-01/examples/BlobContainersGetWithAllowProtectedAppendWritesAll.json
 */
async function getBlobContainersGetWithAllowProtectedAppendWritesAll() {
  const subscriptionId = "{subscription-id}";
  const resourceGroupName = "res9871";
  const accountName = "sto6217";
  const containerName = "container1634";
  const credential = new DefaultAzureCredential();
  const client = new StorageManagementClient(credential, subscriptionId);
  const result = await client.blobContainers.get(resourceGroupName, accountName, containerName);
  console.log(result);
}

getBlobContainersGetWithAllowProtectedAppendWritesAll().catch(console.error);

/**
 * This sample demonstrates how to Gets properties of a specified container.
 *
 * @summary Gets properties of a specified container.
 * x-ms-original-file: specification/storage/resource-manager/Microsoft.Storage/stable/2022-09-01/examples/BlobContainersGet.json
 */
async function getContainers() {
  const subscriptionId = "{subscription-id}";
  const resourceGroupName = "res9871";
  const accountName = "sto6217";
  const containerName = "container1634";
  const credential = new DefaultAzureCredential();
  const client = new StorageManagementClient(credential, subscriptionId);
  const result = await client.blobContainers.get(resourceGroupName, accountName, containerName);
  console.log(result);
}

getContainers().catch(console.error);
'''

        lines = code.splitlines(keepends=True)

        aggregated_js_example = break_down_aggregated_js_example(lines)
        self.assertEqual(2, len(aggregated_js_example.methods))

        self.assertEqual('* This sample demonstrates how to Gets properties of a specified container.', aggregated_js_example.methods[0].content[1].strip())
        self.assertEqual('async function getBlobContainersGetWithAllowProtectedAppendWritesAll() {', aggregated_js_example.methods[0].content[6].strip())
        self.assertEqual('getBlobContainersGetWithAllowProtectedAppendWritesAll().catch(console.error);', aggregated_js_example.methods[0].content[-1].strip())

        self.assertEqual('async function getContainers() {', aggregated_js_example.methods[1].content[6].strip())
        self.assertEqual('getContainers().catch(console.error);', aggregated_js_example.methods[1].content[-1].strip())

    def test_break_down_aggregated_js_example_new_style_multiple(self):
        code = '''const { SynapseManagementClient } = require("@azure/arm-synapse");
const { DefaultAzureCredential } = require("@azure/identity");
require("dotenv").config();

/**
 * This sample demonstrates how to Creates or updates a Sql pool data masking rule.
 *
 * @summary Creates or updates a Sql pool data masking rule.
 * x-ms-original-file: specification/synapse/resource-manager/Microsoft.Synapse/stable/2021-06-01/examples/DataMaskingRuleCreateOrUpdateDefaultMax.json
 */
async function createOrUpdateDataMaskingRuleForDefaultMax() {
  const subscriptionId =
    process.env["SYNAPSE_SUBSCRIPTION_ID"] || "00000000-1111-2222-3333-444444444444";
  const resourceGroupName = process.env["SYNAPSE_RESOURCE_GROUP"] || "sqlcrudtest-6852";
  const workspaceName = "sqlcrudtest-2080";
  const sqlPoolName = "sqlcrudtest-331";
  const dataMaskingRuleName = "rule1";
  const parameters = {
    aliasName: "nickname",
    columnName: "test1",
    maskingFunction: "Default",
    ruleState: "Enabled",
    schemaName: "dbo",
    tableName: "Table_1",
  };
  const credential = new DefaultAzureCredential();
  const client = new SynapseManagementClient(credential, subscriptionId);
  const result = await client.dataMaskingRules.createOrUpdate(
    resourceGroupName,
    workspaceName,
    sqlPoolName,
    dataMaskingRuleName,
    parameters
  );
  console.log(result);
}

/**
 * This sample demonstrates how to Creates or updates a Sql pool data masking rule.
 *
 * @summary Creates or updates a Sql pool data masking rule.
 * x-ms-original-file: specification/synapse/resource-manager/Microsoft.Synapse/stable/2021-06-01/examples/DataMaskingRuleCreateOrUpdateDefaultMin.json
 */
async function createOrUpdateDataMaskingRuleForDefaultMin() {
  const subscriptionId =
    process.env["SYNAPSE_SUBSCRIPTION_ID"] || "00000000-1111-2222-3333-444444444444";
  const resourceGroupName = process.env["SYNAPSE_RESOURCE_GROUP"] || "sqlcrudtest-6852";
  const workspaceName = "sqlcrudtest-2080";
  const sqlPoolName = "sqlcrudtest-331";
  const dataMaskingRuleName = "rule1";
  const parameters = {
    columnName: "test1",
    maskingFunction: "Default",
    schemaName: "dbo",
    tableName: "Table_1",
  };
  const credential = new DefaultAzureCredential();
  const client = new SynapseManagementClient(credential, subscriptionId);
  const result = await client.dataMaskingRules.createOrUpdate(
    resourceGroupName,
    workspaceName,
    sqlPoolName,
    dataMaskingRuleName,
    parameters
  );
  console.log(result);
}

/**
 * This sample demonstrates how to Creates or updates a Sql pool data masking rule.
 *
 * @summary Creates or updates a Sql pool data masking rule.
 * x-ms-original-file: specification/synapse/resource-manager/Microsoft.Synapse/stable/2021-06-01/examples/DataMaskingRuleCreateOrUpdateNumber.json
 */
async function createOrUpdateDataMaskingRuleForNumbers() {
  const subscriptionId =
    process.env["SYNAPSE_SUBSCRIPTION_ID"] || "00000000-1111-2222-3333-444444444444";
  const resourceGroupName = process.env["SYNAPSE_RESOURCE_GROUP"] || "sqlcrudtest-6852";
  const workspaceName = "sqlcrudtest-2080";
  const sqlPoolName = "sqlcrudtest-331";
  const dataMaskingRuleName = "rule1";
  const parameters = {
    columnName: "test1",
    maskingFunction: "Number",
    numberFrom: "0",
    numberTo: "2",
    schemaName: "dbo",
    tableName: "Table_1",
  };
  const credential = new DefaultAzureCredential();
  const client = new SynapseManagementClient(credential, subscriptionId);
  const result = await client.dataMaskingRules.createOrUpdate(
    resourceGroupName,
    workspaceName,
    sqlPoolName,
    dataMaskingRuleName,
    parameters
  );
  console.log(result);
}

/**
 * This sample demonstrates how to Creates or updates a Sql pool data masking rule.
 *
 * @summary Creates or updates a Sql pool data masking rule.
 * x-ms-original-file: specification/synapse/resource-manager/Microsoft.Synapse/stable/2021-06-01/examples/DataMaskingRuleCreateOrUpdateText.json
 */
async function createOrUpdateDataMaskingRuleForText() {
  const subscriptionId =
    process.env["SYNAPSE_SUBSCRIPTION_ID"] || "00000000-1111-2222-3333-444444444444";
  const resourceGroupName = process.env["SYNAPSE_RESOURCE_GROUP"] || "sqlcrudtest-6852";
  const workspaceName = "sqlcrudtest-2080";
  const sqlPoolName = "sqlcrudtest-331";
  const dataMaskingRuleName = "rule1";
  const parameters = {
    columnName: "test1",
    maskingFunction: "Text",
    prefixSize: "1",
    replacementString: "asdf",
    schemaName: "dbo",
    suffixSize: "0",
    tableName: "Table_1",
  };
  const credential = new DefaultAzureCredential();
  const client = new SynapseManagementClient(credential, subscriptionId);
  const result = await client.dataMaskingRules.createOrUpdate(
    resourceGroupName,
    workspaceName,
    sqlPoolName,
    dataMaskingRuleName,
    parameters
  );
  console.log(result);
}

async function main() {
  createOrUpdateDataMaskingRuleForDefaultMax();
  createOrUpdateDataMaskingRuleForDefaultMin();
  createOrUpdateDataMaskingRuleForNumbers();
  createOrUpdateDataMaskingRuleForText();
}

main().catch(console.error);
'''

        lines = code.splitlines(keepends=True)

        aggregated_js_example = break_down_aggregated_js_example(lines)
        self.assertEqual(4, len(aggregated_js_example.methods))

        self.assertEqual('async function createOrUpdateDataMaskingRuleForDefaultMax() {', aggregated_js_example.methods[0].content[6].strip())

    def test_break_down_aggregated_js_example_new_style_single(self):
        code = '''const { SynapseManagementClient } = require("@azure/arm-synapse");
const { DefaultAzureCredential } = require("@azure/identity");
require("dotenv").config();

/**
 * This sample demonstrates how to Lists auditing settings of a Sql pool.
 *
 * @summary Lists auditing settings of a Sql pool.
 * x-ms-original-file: specification/synapse/resource-manager/Microsoft.Synapse/stable/2021-06-01/examples/SqlPoolAuditingSettingsList.json
 */
async function listAuditSettingsOfADatabase() {
  const subscriptionId =
    process.env["SYNAPSE_SUBSCRIPTION_ID"] || "00000000-1111-2222-3333-444444444444";
  const resourceGroupName = process.env["SYNAPSE_RESOURCE_GROUP"] || "blobauditingtest-6852";
  const workspaceName = "blobauditingtest-2080";
  const sqlPoolName = "testdb";
  const credential = new DefaultAzureCredential();
  const client = new SynapseManagementClient(credential, subscriptionId);
  const resArray = new Array();
  for await (let item of client.sqlPoolBlobAuditingPolicies.listBySqlPool(
    resourceGroupName,
    workspaceName,
    sqlPoolName
  )) {
    resArray.push(item);
  }
  console.log(resArray);
}

async function main() {
  listAuditSettingsOfADatabase();
}

main().catch(console.error);
'''

        lines = code.splitlines(keepends=True)

        aggregated_js_example = break_down_aggregated_js_example(lines)
        self.assertEqual(1, len(aggregated_js_example.methods))

        self.assertEqual(3, len(aggregated_js_example.class_opening))

        self.assertEqual('async function listAuditSettingsOfADatabase() {',
                         aggregated_js_example.methods[0].content[6].strip())
        self.assertEqual('}', aggregated_js_example.methods[0].content[-1].rstrip())

        example_lines = aggregated_js_example.class_opening + aggregated_js_example.methods[0].content
        example_lines = format_js(example_lines)

    @unittest.skip
    def test_get_module_relative_path(self):
        sdk_path = 'c:/github/azure-sdk-for-js'
        sdk_name = 'mysql-flexible'
        module_relative_path = get_module_relative_path(sdk_name, PackageType.HLC, sdk_path)
        self.assertEqual('sdk/mysql/azure-mysql-flexible', module_relative_path)

    @unittest.skip
    def test_create_js_examples(self):
        release = Release('@azure/arm-policyinsights_6.0.0-beta.1',
                          '@azure/arm-policyinsights',
                          '6.0.0-beta.1')
        js_module = f'{release.package}@{release.version}'
        sdk_examples_path = 'c:/github/azure-rest-api-specs-examples'
        js_examples_path = 'c:/github/azure-sdk-for-js/sdk/policyinsights/arm-policyinsights/samples/v6-beta/javascript'
        succeeded, files = create_js_examples(release, js_module, sdk_examples_path, js_examples_path)
        self.assertTrue(succeeded)
