# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import List, Optional
from .primitive_schemas import StringSchema
from .parameter import Parameter, ParameterLocation
from .parameter_list import GlobalParameterList
from .imports import FileImport, ImportType, TypingSection


class Client:
    """A service client.
    """

    def __init__(self, code_model, parameters: GlobalParameterList):
        self.code_model = code_model
        self.parameters = parameters
        self.base_url: Optional[str] = None
        self.custom_base_url = None
        self._config_parameters = parameters

    def pipeline_class(self, async_mode: bool) -> str:
        if self.code_model.options["azure_arm"]:
            if async_mode:
                return "AsyncARMPipelineClient"
            return "ARMPipelineClient"
        if async_mode:
            return "AsyncPipelineClient"
        return "PipelineClient"

    def _imports_shared(self, async_mode: bool) -> FileImport:
        file_import = FileImport()

        file_import.add_from_import("msrest", "Serializer", ImportType.AZURECORE)
        file_import.add_from_import("msrest", "Deserializer", ImportType.AZURECORE)
        file_import.add_from_import("typing", "Any", ImportType.STDLIB, TypingSection.CONDITIONAL)

        any_optional_gp = any(not gp.required for gp in self.parameters)

        if any_optional_gp or self.code_model.service_client.base_url:
            file_import.add_from_import("typing", "Optional", ImportType.STDLIB, TypingSection.CONDITIONAL)

        if self.code_model.options["azure_arm"]:
            file_import.add_from_import(
                "azure.mgmt.core", self.pipeline_class(async_mode), ImportType.AZURECORE
            )
        else:
            file_import.add_from_import(
                "azure.core", self.pipeline_class(async_mode), ImportType.AZURECORE
            )

        for gp in self.code_model.global_parameters:
            file_import.merge(gp.imports())
        file_import.add_from_import(
            "._configuration", f"{self.code_model.class_name}Configuration",
            ImportType.LOCAL
        )

        return file_import

    def imports(self, async_mode: bool) -> FileImport:
        file_import = self._imports_shared(async_mode)
        if async_mode:
            file_import.add_from_import("typing", "Awaitable", ImportType.STDLIB)
            file_import.add_from_import(
                "azure.core.rest", "AsyncHttpResponse", ImportType.AZURECORE, TypingSection.CONDITIONAL
            )
        else:
            file_import.add_from_import(
                "azure.core.rest", "HttpResponse", ImportType.AZURECORE, TypingSection.CONDITIONAL
            )
        file_import.add_from_import("azure.core.rest", "HttpRequest", ImportType.AZURECORE, TypingSection.CONDITIONAL)
        for og in self.code_model.operation_groups:
            file_import.add_from_import(".operations", og.class_name, ImportType.LOCAL)

        if self.code_model.sorted_schemas:
            path_to_models = ".." if async_mode else "."
            file_import.add_from_import(path_to_models, "models", ImportType.LOCAL)
        else:
            # in this case, we have client_models = {} in the service client, which needs a type annotation
            # this import will always be commented, so will always add it to the typing section
            file_import.add_from_import("typing", "Dict", ImportType.STDLIB, TypingSection.TYPING)
        file_import.add_from_import("copy", "deepcopy", ImportType.STDLIB)
        return file_import

    def imports_for_multiapi(self, async_mode: bool) -> FileImport:
        file_import = self._imports_shared(async_mode)
        try:
            mixin_operation = next(og for og in self.code_model.operation_groups if og.is_empty_operation_group)
            file_import.add_from_import("._operations_mixin", mixin_operation.class_name, ImportType.LOCAL)
        except StopIteration:
            pass
        return file_import

    @property
    def method_parameters(self) -> List[Parameter]:
        base_url_param = []
        if self.base_url:
            base_url_param = [Parameter(
                yaml_data={},
                schema=StringSchema(namespace="", yaml_data={"type": "str"}),
                rest_api_name="base_url",
                serialized_name="base_url",
                description="Service URL",
                implementation="Client",
                required=False,
                location=ParameterLocation.Other,
                skip_url_encoding=False,
                constraints=[],
            )]
        return self.parameters.method + base_url_param

    def method_parameters_signature(self, async_mode) -> List[str]:
        return [
            parameter.method_signature(async_mode) for parameter in self.method_parameters
        ] + self.parameters.method_signature_kwargs(async_mode)

    def send_request_signature(self, async_mode) -> List[str]:
        request_signature = ["request: HttpRequest," if async_mode else "request,  # type: HttpRequest"]
        return request_signature + self.parameters.method_signature_kwargs(async_mode)

    @property
    def config_initialization(self) -> str:
        method = ", ".join([p.serialized_name for p in self.parameters.method])
        if method:
            return method + ", **kwargs"
        return "**kwargs"
