# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import functools
import copy
import json
from typing import List, Optional, Set, Tuple, Dict, Union
from jinja2 import Environment
from .general_serializer import config_imports
from ..models import (
    CodeModel,
    FileImport,
    OperationGroup,
    LROOperation,
    PagingOperation,
    TokenCredentialSchema,
    ParameterList,
    TypingSection,
    ImportType,
    GlobalParameterList,
)
from .builder_serializer import get_operation_serializer


def _correct_credential_parameter(global_parameters: ParameterList, async_mode: bool) -> None:
    credential_param = [gp for gp in global_parameters.parameters if isinstance(gp.schema, TokenCredentialSchema)][0]
    credential_param.schema = TokenCredentialSchema(async_mode=async_mode)


def _json_serialize_imports(
    imports: Dict[TypingSection, Dict[ImportType, Dict[str, Set[Optional[Union[str, Tuple[str, str]]]]]]]
):
    if not imports:
        return None

    json_serialize_imports = {}
    # need to make name_import set -> list to make the dictionary json serializable
    # not using an OrderedDict since we're iterating through a set and the order there varies
    # going to sort the list instead

    for typing_section_key, typing_section_value in imports.items():
        json_import_type_dictionary = {}
        for import_type_key, import_type_value in typing_section_value.items():
            json_package_name_dictionary = {}
            for package_name, name_imports in import_type_value.items():
                name_import_ordered_list = []
                if name_imports:
                    name_import_ordered_list = list(name_imports)
                    name_import_ordered_list.sort()
                json_package_name_dictionary[package_name] = name_import_ordered_list
            json_import_type_dictionary[import_type_key] = json_package_name_dictionary
        json_serialize_imports[typing_section_key] = json_import_type_dictionary
    return json.dumps(json_serialize_imports)


def _mixin_imports(mixin_operation_group: Optional[OperationGroup]) -> Tuple[Optional[str], Optional[str]]:
    if not mixin_operation_group:
        return None, None

    sync_mixin_imports = mixin_operation_group.imports_for_multiapi(async_mode=False)
    async_mixin_imports = mixin_operation_group.imports_for_multiapi(async_mode=True)

    return _json_serialize_imports(sync_mixin_imports.imports), _json_serialize_imports(async_mixin_imports.imports)


class MetadataSerializer:
    def __init__(self, code_model: CodeModel, env: Environment) -> None:
        self.code_model = code_model
        self.env = env

    def _choose_api_version(self) -> Tuple[str, List[str]]:
        chosen_version = ""
        total_api_version_set: Set[str] = set()
        for operation_group in self.code_model.operation_groups:
            total_api_version_set.update(operation_group.api_versions)

        total_api_version_list = list(total_api_version_set)
        total_api_version_list.sort()

        # switching ' to " so json can decode the dict we end up writing to file
        total_api_version_list = [str(api_version).replace("'", '"') for api_version in total_api_version_list]
        if len(total_api_version_list) == 1:
            chosen_version = total_api_version_list[0]
        elif len(total_api_version_list) > 1:
            module_version = self.code_model.namespace.split(".")[-1]
            for api_version in total_api_version_list:
                if "v{}".format(api_version.replace("-", "_")) == module_version:
                    chosen_version = api_version

        return chosen_version, total_api_version_list

    def _make_async_copy_of_global_parameters(self) -> GlobalParameterList:
        global_parameters = copy.deepcopy(self.code_model.global_parameters)
        _correct_credential_parameter(global_parameters, True)
        return global_parameters

    def _service_client_imports(
        self, global_parameters: ParameterList, mixin_operation_group: Optional[OperationGroup], async_mode: bool
    ) -> str:
        file_import = FileImport()
        for gp in global_parameters:
            file_import.merge(gp.imports())
        file_import.add_from_import("azure.profiles", "KnownProfiles", import_type=ImportType.AZURECORE)
        file_import.add_from_import("azure.profiles", "ProfileDefinition", import_type=ImportType.AZURECORE)
        file_import.add_from_import(
            "azure.profiles.multiapiclient", "MultiApiClientMixin", import_type=ImportType.AZURECORE
        )
        file_import.add_from_import("._configuration", f"{self.code_model.class_name}Configuration", ImportType.LOCAL)
        # api_version and potentially base_url require Optional typing
        file_import.add_from_import("typing", "Optional", ImportType.STDLIB, TypingSection.CONDITIONAL)
        if mixin_operation_group:
            file_import.add_from_import(
                "._operations_mixin", f"{self.code_model.class_name}OperationsMixin", ImportType.LOCAL
            )
        file_import.merge(self.code_model.service_client.imports_for_multiapi(async_mode=async_mode))
        return _json_serialize_imports(file_import.imports)

    def serialize(self) -> str:
        def _is_lro(operation):
            return isinstance(operation, LROOperation)

        def _is_paging(operation):
            return isinstance(operation, PagingOperation)

        mixin_operation_group: Optional[OperationGroup] = next(
            (
                operation_group
                for operation_group in self.code_model.operation_groups
                if operation_group.is_empty_operation_group
            ),
            None,
        )
        mixin_operations = mixin_operation_group.operations if mixin_operation_group else []
        sync_mixin_imports, async_mixin_imports = _mixin_imports(mixin_operation_group)

        chosen_version, total_api_version_list = self._choose_api_version()

        # we separate out async and sync for the case of credentials.
        # In this case, we need two copies of the credential global parameter
        # for typing purposes.
        async_global_parameters = self.code_model.global_parameters
        if (
            self.code_model.options['credential'] and
            isinstance(self.code_model.credential_schema_policy.credential, TokenCredentialSchema)
        ):
            # this ensures that the TokenCredentialSchema showing up in the list of code model's global parameters
            # is sync. This way we only have to make a copy for an async_credential
            _correct_credential_parameter(self.code_model.global_parameters, False)
            async_global_parameters = self._make_async_copy_of_global_parameters()

        sync_client_imports = self._service_client_imports(
            self.code_model.global_parameters, mixin_operation_group, async_mode=False
        )
        async_client_imports = self._service_client_imports(
            async_global_parameters, mixin_operation_group, async_mode=True
        )

        template = self.env.get_template("metadata.json.jinja2")

        # setting to true, because for multiapi we always generate with a version file with version 0.1.0
        self.code_model.options["package_version"] = "0.1.0"
        if self.code_model.options["azure_arm"] and not self.code_model.service_client.base_url:
            self.code_model.service_client.base_url = "https://management.azure.com"
        return template.render(
            chosen_version=chosen_version,
            total_api_version_list=total_api_version_list,
            code_model=self.code_model,
            sync_global_parameters=self.code_model.global_parameters,
            async_global_parameters=async_global_parameters,
            mixin_operations=mixin_operations,
            any=any,
            is_lro=_is_lro,
            is_paging=_is_paging,
            str=str,
            sync_mixin_imports=sync_mixin_imports,
            async_mixin_imports=async_mixin_imports,
            sync_client_imports=sync_client_imports,
            async_client_imports=async_client_imports,
            sync_config_imports=_json_serialize_imports(
                config_imports(self.code_model, self.code_model.global_parameters, async_mode=False).imports
            ),
            async_config_imports=_json_serialize_imports(
                config_imports(self.code_model, async_global_parameters, async_mode=True).imports
            ),
            get_async_operation_serializer=functools.partial(
                get_operation_serializer, code_model=self.code_model, async_mode=True
            ),
            get_sync_operation_serializer=functools.partial(
                get_operation_serializer, code_model=self.code_model, async_mode=False
            ),
        )
