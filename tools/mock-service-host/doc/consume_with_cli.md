# Consume Mock Service Host With Azure CLI

## Use official client

### Install Azure CLI

Follow the [guide](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) to install Azure CLI.
### Activate the Python environment

Run `'az --version'` to get which python environment is used in your computer, for instance:
![az-version.png](pic/az-version.png)

According to above output, you know the CLI is using Python virtual environment c:\ZZ\projects\codegen\venv_mock, saying it's YOUR_VENV.
Then you need to activate this venv:

```
# . <YOUR_VENV>/bin/activate       // for LINUX/mac
or
> <YOUR_VENV>\Scripts\activate    // for Windows
```

> **_NOTE:_** If your Azure CLI is installed with no python virtual environment (a system Python or a CLI embeded Python), don't need to activate any VENV, but make sure the following step will be executed with that Python folder.

### Trust the certificate of Mock Service Host in Python environment

The Mock Service Host use a self-signed certificate which will be appended into the file **cacert.pem** in your Python environment.

```shell
 cat .ssh/localhost-ca.crt >> <YOUR_VENV>/lib/python3.8/site-packages/certifi/cacert.pem       // for LINUX/mac
```

or

```bat
type .ssh\localhost-ca.crt >> <YOUR_VENV>\Lib\site-packages\certifi\cacert.pem                // for Windows
```

### Configure Azure CLI to use Mock Service Host

```shell
# az login --server-principal --username <USERNAME> --password <PASSWORD> --tenant <TENANT> // login with any realworld credential
# az cloud register -n virtualCloud
                    --endpoint-resource-manager "https://localhost:8443"                    // connect to stateless endpoint
                    --endpoint-active-directory https://login.microsoftonline.com
                    --endpoint-active-directory-graph-resource-id https://graph.windows.net/
                    --endpoint-active-directory-resource-id https://management.core.windows.net/
# az cloud set -n virtualCloud
```

### Request with Azure CLI

Now you can try any Azure CLI command, the setup is done if mocked response is received for the below command.

```shell
# az network vnet peering create --allow-vnet-access --name MyVnet1ToMyVnet2 --remote-vnet MyVnet2Id --resource-group MyResourceGroup --vnet-name MyVnet1

{
  "allowForwardedTraffic": true,
  "allowGatewayTransit": true,
  "allowVirtualNetworkAccess": true,
  "etag": "aaaaaaaaaaaaaaaaaaa",
  "id": "aaaa",
  "name": "MyVnet1ToMyVnet2",
  "peeringState": "Initiated",
  "provisioningState": "Succeeded",
  "remoteAddressSpace": {
    "addressPrefixes": [
      "aaaaaaaaaaaaaaaaaaaaa"
    ]
  },
  "remoteBgpCommunities": {
    "regionalCommunity": "aaaaaaaaaaaaaaaaaaaaaa",
    "virtualNetworkCommunity": "aaaaaaaaaaaaa"
  },
  "remoteVirtualNetwork": {
    "id": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
  },
  "type": "aaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "useRemoteGateways": true
}
```

## Generate your own client

> **_NOTE:_** The [autorest](https://www.npmjs.com/package/autorest) is required for the following step.

### Generation
Following the [Autorest.Az Guide](https://github.com/Azure/autorest.az#how-to-use-azure-cli-code-generator) you can generate CLI extension with your own swagger files. Key steps are:

```diff
# autorest  --az
            --use=https://trenton.blob.core.windows.net/trenton/autorest-az-1.7.3.tgz
            <path-to-the-swagger-readme.md>
            --azure-cli-extension-folder=<output-folder>
+           --gen-cmdlet-test               // remember to add this option if want to run tests
# cd <output-folder>/<extension-folder>
# python setup.py sdist bdist_wheel         // generate wheel-file (*.whl) in "dist" folder
# az extension add --source=<path-to-the-wheel-file>
```

Now you can try to look through and run your extended Azure CLI command.

```shell
# az <extension-name>  --help               // check generated comand groups
# az <extension-name> <group-name> --help  // check commands in the group
# az <extension-name> <group-name> <create|list|show|delete...> --help  // check detail command information
# az <extension-name> <group-name> <create|list|show|delete...> <--params...>  // run your command with Virtual Server
```
- Example: guestconfiguration

Below is sample steps for generate CLI extension for RP [guestconfiguration](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/guestconfiguration/resource-manager):

```bat
> autorest  --az
            --use=https://trenton.blob.core.windows.net/trenton/autorest-az-1.7.3.tgz
            ..\azure-rest-api-specs\specification\guestconfiguration\resource-manager\readme.md
            --azure-cli-extension-folder=..\generated
            --gen-cmdlet-test
# cd ..\generated                     // go to the output folder
# cd src\guestconfig                  // go to the generated extension folder
# python setup.py sdist bdist_wheel
# az extension remove guestconfig     // remove it first since it's an existing extension
# az extension add --source=.\dist\guestconfig-0.1.0-py3-none-any.whl

# az guestconfig --help               // check information for the guestconfig extension.
...
```

> **_NOTE:_** The variable <extension-name> can be found in readme.az.md. For guestconfiguration, it's ["extensions: guestconfig"](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/guestconfiguration/resource-manager/readme.az.md#az)

### Testing

Testcases are also generated for the CLI extension, you can run the end-to-end test case with Mock Service Host in one command.
The test file is located at `<output-folder>\src\<extension-name>\azext_<extension-name>\tests\cmdlet\test_positive.py`
- Example: guestconfiguration
Taking RP guestconfiguration as example, the test can be run with below command:

```bat
> pip install pytest        // make sure pytest is installed
> pytest -rA <output-folder>\src\guestconfig\azext_guestconfig\tests\cmdlet\test_positive.py
...
========================= short test summary info ==========================
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_report_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_report_show
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_show
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp2
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp_assignment_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp_assignment_show
========================= 8 passed, 3 warnings in 36.24s =========================
```