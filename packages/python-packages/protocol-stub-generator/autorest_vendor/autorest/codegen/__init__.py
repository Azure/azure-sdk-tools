# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import logging
import sys
from typing import Dict, Any, Set, Union, List, Type
import yaml

from .. import Plugin
from .models.code_model import CodeModel
from .models import build_schema
from .models.operation_group import OperationGroup
from .models.parameter import Parameter
from .models.parameter_list import GlobalParameterList
from .models.rest import Rest
from .serializers import JinjaSerializer
from .models.credential_schema_policy import CredentialSchemaPolicy, get_credential_schema_policy_type
from .models.credential_schema import AzureKeyCredentialSchema, TokenCredentialSchema

def _build_convenience_layer(yaml_data: Dict[str, Any], code_model: CodeModel) -> None:
    # Create operations
    if code_model.show_operations and yaml_data.get("operationGroups"):
        code_model.operation_groups = [
            OperationGroup.from_yaml(code_model, op_group) for op_group in yaml_data["operationGroups"]
        ]
    if code_model.show_models and yaml_data.get("schemas"):
        # sets the enums property in our code_model variable, which will later be passed to EnumSerializer

        code_model.add_inheritance_to_models()
        code_model.sort_schemas()
        code_model.link_operation_to_request_builder()
        code_model.add_schema_link_to_operation()
        code_model.generate_single_parameter_from_multiple_media_types_operation()

    if code_model.show_operations:
        # LRO operation
        code_model.format_lro_operations()
        code_model.remove_next_operation()

_LOGGER = logging.getLogger(__name__)
class CodeGenerator(Plugin):
    @staticmethod
    def remove_cloud_errors(yaml_data: Dict[str, Any]) -> None:
        for group in yaml_data["operationGroups"]:
            for operation in group["operations"]:
                if not operation.get("exceptions"):
                    continue
                i = 0
                while i < len(operation["exceptions"]):
                    exception = operation["exceptions"][i]
                    if exception.get("schema") and exception["schema"]["language"]["default"]["name"] == "CloudError":
                        del operation["exceptions"][i]
                        i -= 1
                    i += 1
        if yaml_data.get("schemas") and yaml_data["schemas"].get("objects"):
            for i in range(len(yaml_data["schemas"]["objects"])):
                obj_schema = yaml_data["schemas"]["objects"][i]
                if obj_schema["language"]["default"]["name"] == "CloudError":
                    del yaml_data["schemas"]["objects"][i]
                    break

    @staticmethod
    def _build_exceptions_set(yaml_data: List[Dict[str, Any]]) -> Set[int]:
        exceptions_set = set()
        for group in yaml_data:
            for operation in group["operations"]:
                if not operation.get("exceptions"):
                    continue
                for exception in operation["exceptions"]:
                    if not exception.get("schema"):
                        continue
                    exceptions_set.add(id(exception["schema"]))
        return exceptions_set

    def _create_code_model(self, yaml_data: Dict[str, Any], options: Dict[str, Union[str, bool]]) -> CodeModel:
        # Create a code model
        low_level_client = self._autorestapi.get_boolean_value("low-level-client", False)
        show_models = self._autorestapi.get_boolean_value(
            "show-models",
            not low_level_client
        )
        show_builders = self._autorestapi.get_boolean_value(
            "show-builders",
            low_level_client
        )
        show_operations = self._autorestapi.get_boolean_value(
            "show-operations",
            not low_level_client
        )
        show_send_request = self._autorestapi.get_boolean_value(
            "show-send-request",
            low_level_client
        )
        only_path_and_body_params_positional = self._autorestapi.get_boolean_value(
            "only-path-and-body-params-positional",
            low_level_client
        )
        code_model = CodeModel(
            show_builders=show_builders,
            show_models=show_models,
            show_operations=show_operations,
            show_send_request=show_send_request,
            only_path_and_body_params_positional=only_path_and_body_params_positional,
            options=options,
        )
        if code_model.options['credential']:
            self._handle_default_authentication_policy(code_model)
        code_model.module_name = yaml_data["info"]["python_title"]
        code_model.class_name = yaml_data["info"]["pascal_case_title"]
        code_model.description = (
            yaml_data["info"]["description"] if yaml_data["info"].get("description") else ""
        )

        # Global parameters
        code_model.global_parameters = GlobalParameterList(
            [Parameter.from_yaml(param) for param in yaml_data.get("globalParameters", [])],
        )

        # Custom URL
        dollar_host = [parameter for parameter in code_model.global_parameters if parameter.rest_api_name == "$host"]
        if not dollar_host:
            # We don't want to support multi-api customurl YET (will see if that goes well....)
            # So far now, let's get the first one in the first operation
            # UGLY as hell.....
            if yaml_data.get("operationGroups"):
                first_req_of_first_op_of_first_grp = yaml_data["operationGroups"][0]["operations"][0]["requests"][0]
                code_model.service_client.custom_base_url = (
                    first_req_of_first_op_of_first_grp["protocol"]["http"]["uri"]
                )
        else:
            for host in dollar_host:
                code_model.global_parameters.remove(host)
            code_model.service_client.base_url = dollar_host[0].yaml_data["clientDefaultValue"]

        # Get my namespace
        namespace = self._autorestapi.get_value("namespace")
        _LOGGER.debug("Namespace parameter was %s", namespace)
        if not namespace:
            namespace = yaml_data["info"]["python_title"]
        code_model.namespace = namespace

        code_model.rest = Rest.from_yaml(yaml_data, code_model=code_model)
        if yaml_data.get("schemas"):
            exceptions_set = CodeGenerator._build_exceptions_set(yaml_data=yaml_data["operationGroups"])

            for type_list in yaml_data["schemas"].values():
                for schema in type_list:
                    build_schema(yaml_data=schema, exceptions_set=exceptions_set, code_model=code_model)
            code_model.add_schema_link_to_request_builder()
            code_model.add_schema_link_to_global_parameters()

        _build_convenience_layer(yaml_data=yaml_data, code_model=code_model)

        if options["credential"]:
            code_model.add_credential_global_parameter()

        return code_model

    def _get_credential_scopes(self, credential):
        credential_scopes_temp = self._autorestapi.get_value("credential-scopes")
        credential_scopes = credential_scopes_temp.split(",") if credential_scopes_temp else None
        if credential_scopes and not credential:
            raise ValueError("--credential-scopes must be used with the --add-credential flag")

        # check to see if user just passes in --credential-scopes with no value
        if self._autorestapi.get_boolean_value("credential-scopes", False) and not credential_scopes:
            raise ValueError(
                "--credential-scopes takes a list of scopes in comma separated format. "
                "For example: --credential-scopes=https://cognitiveservices.azure.com/.default"
            )
        return credential_scopes

    def _initialize_credential_schema_policy(
        self, code_model: CodeModel, credential_schema_policy: Type[CredentialSchemaPolicy]
    ) -> CredentialSchemaPolicy:
        credential_scopes = self._get_credential_scopes(code_model.options['credential'])
        credential_key_header_name = self._autorestapi.get_value('credential-key-header-name')
        azure_arm = code_model.options['azure_arm']
        credential = code_model.options['credential']

        if hasattr(credential_schema_policy, "credential_scopes"):
            if not credential_scopes:
                if azure_arm:
                    credential_scopes = ["https://management.azure.com/.default"]
                elif credential:
                    # If add-credential is specified, we still want to add a credential_scopes variable.
                    # Will make it an empty list so we can differentiate between this case and None
                    _LOGGER.warning(
                        "You have default credential policy %s "
                        "but not the --credential-scopes flag set while generating non-management plane code. "
                        "This is not recommend because it forces the customer to pass credential scopes "
                        "through kwargs if they want to authenticate.",
                        credential_schema_policy.name()
                    )
                    credential_scopes = []

            if credential_key_header_name:
                raise ValueError(
                    "You have passed in a credential key header name with default credential policy type "
                    f"{credential_schema_policy.name()}. This is not allowed, since credential key header "
                    "name is tied with AzureKeyCredentialPolicy. Instead, with this policy it is recommend you "
                    "pass in --credential-scopes."
                )
            return credential_schema_policy(
                credential=TokenCredentialSchema(async_mode=False),
                credential_scopes=credential_scopes,
            )
        # currently the only other credential policy is AzureKeyCredentialPolicy
        if credential_scopes:
            raise ValueError(
                "You have passed in credential scopes with default credential policy type "
                "AzureKeyCredentialPolicy. This is not allowed, since credential scopes is tied with "
                f"{code_model.default_authentication_policy.name()}. Instead, with this policy you must pass in "
                "--credential-key-header-name."
            )
        if not credential_key_header_name:
            credential_key_header_name = "api-key"
            _LOGGER.info(
                "Defaulting the AzureKeyCredentialPolicy header's name to 'api-key'"
            )
        return credential_schema_policy(
            credential=AzureKeyCredentialSchema(),
            credential_key_header_name=credential_key_header_name,
        )

    def _handle_default_authentication_policy(self, code_model: CodeModel):
        credential_schema_policy_name = (
            self._autorestapi.get_value("credential-default-policy-type") or
            code_model.default_authentication_policy.name()
        )
        credential_schema_policy_type = get_credential_schema_policy_type(credential_schema_policy_name)
        credential_schema_policy = self._initialize_credential_schema_policy(
            code_model, credential_schema_policy_type
        )
        code_model.credential_schema_policy = credential_schema_policy


    def _build_code_model_options(self) -> Dict[str, Any]:
        """Build en options dict from the user input while running autorest.
        """
        azure_arm = self._autorestapi.get_boolean_value("azure-arm", False)
        credential = (
            self._autorestapi.get_boolean_value("add-credentials", False) or
            self._autorestapi.get_boolean_value("add-credential", False)
        )

        license_header = self._autorestapi.get_value("header-text")
        if license_header:
            license_header = license_header.replace("\n", "\n# ")
            license_header = (
                "# --------------------------------------------------------------------------\n# " + license_header
            )
            license_header += "\n# --------------------------------------------------------------------------"

        options: Dict[str, Any] = {
            "azure_arm": azure_arm,
            "credential": credential,
            "head_as_boolean": self._autorestapi.get_boolean_value("head-as-boolean", False),
            "license_header": license_header,
            "keep_version_file": self._autorestapi.get_boolean_value("keep-version-file", False),
            "no_async": self._autorestapi.get_boolean_value("no-async", False),
            "no_namespace_folders": self._autorestapi.get_boolean_value("no-namespace-folders", False),
            "basic_setup_py": self._autorestapi.get_boolean_value("basic-setup-py", False),
            "package_name": self._autorestapi.get_value("package-name"),
            "package_version": self._autorestapi.get_value("package-version"),
            "client_side_validation": self._autorestapi.get_boolean_value("client-side-validation", False),
            "tracing": self._autorestapi.get_boolean_value("trace", False),
            "multiapi": self._autorestapi.get_boolean_value("multiapi", False),
            "polymorphic_examples": self._autorestapi.get_value("polymorphic-examples") or 5,
        }

        if options["basic_setup_py"] and not options["package_version"]:
            raise ValueError("--basic-setup-py must be used with --package-version")

        # Force some options in ARM MODE:
        if azure_arm:
            options["credential"] = True
            options["head_as_boolean"] = True
        return options

    def process(self) -> bool:
        # List the input file, should be only one
        inputs = self._autorestapi.list_inputs()
        _LOGGER.debug("Possible Inputs: %s", inputs)
        if "code-model-v4-no-tags.yaml" not in inputs:
            raise ValueError("code-model-v4-no-tags.yaml must be a possible input")

        file_content = self._autorestapi.read_file("code-model-v4-no-tags.yaml")

        # Parse the received YAML
        yaml_data = yaml.safe_load(file_content)

        options = self._build_code_model_options()

        if options["azure_arm"]:
            self.remove_cloud_errors(yaml_data)

        code_model = self._create_code_model(yaml_data=yaml_data, options=options)

        serializer = JinjaSerializer(self._autorestapi)
        serializer.serialize(code_model)

        return True


def main(yaml_model_file: str) -> None:
    from ..jsonrpc.localapi import LocalAutorestAPI  # pylint: disable=import-outside-toplevel

    code_generator = CodeGenerator(autorestapi=LocalAutorestAPI(reachable_files=[yaml_model_file]))
    if not code_generator.process():
        raise SystemExit("Process didn't finish gracefully")


if __name__ == "__main__":
    main(sys.argv[1])
