
# Protocol API View

## Class Descriptions
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
    
## Autorest Dependency 
* Requests and Response Models Depend on Autorestv3: [Autorest.python](https://github.com/Azure/autorest.python/tree/prepare_request)

