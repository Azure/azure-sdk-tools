
# Protocol API View

## Main Class Descriptions
* ProtocolMainView
    * Formats the whole view
    * Creates the Navigation Panel on the side
        * Overview_tokens : overview panel tokens
        * Tokens: details panel tokens 
* ProtocolOperationGroupView
    * Formats the OperationGroups 
    * Calls ProtocolOperationView to populate the operations
* ProtocolOperationView
    * Formats the operations contained in OperationGroups
    * Calls ProtocolParameterView to populate each operations parameters
* ProtocolParameterView
    * Gets the parameters for each operation from the yaml
    * Calls get_type() to obtain their types 

## Request and Response Class

* Request and Response Class
    * request_builder: 
        * Format models of requests and responses using the Autorestv3 CodeGen models for RequestBuilder and Operation Responses.

## How to Run Locally

* Running the package:
    * Navigate to protocol-stub-generator level
    * `pip install -e . `
    * `protocolGen --pkg-path MY_YAML_ABSOLUTE_PATH`

## Autorest Dependency 
* Requests and Response Models Depend on Autorestv3: [Autorest.python](https://github.com/Azure/autorest.python/tree/autorestv3)
* Commit: 3ca50e506c0f75f0f751aff39c69be7513c986e2

