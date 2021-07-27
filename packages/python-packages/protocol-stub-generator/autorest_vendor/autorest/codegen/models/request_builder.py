# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, List, TypeVar, Optional, Callable

from .base_builder import BaseBuilder, get_converted_parameters
from .request_builder_parameter import RequestBuilderParameter
from .request_builder_parameter_list import RequestBuilderParameterList
from .schema_request import SchemaRequest
from .schema_response import SchemaResponse
from .imports import FileImport, ImportType, TypingSection


T = TypeVar('T')
OrderedSet = Dict[T, None]

class RequestBuilder(BaseBuilder):
    def __init__(
        self,
        yaml_data: Dict[str, Any],
        name: str,
        url: str,
        method: str,
        multipart: bool,
        schema_requests: List[SchemaRequest],
        parameters: RequestBuilderParameterList,
        description: str,
        summary: str,
        responses: Optional[List[SchemaResponse]] = None,
    ):
        super().__init__(
            yaml_data=yaml_data,
            name=name,
            description=description,
            parameters=parameters,
            responses=responses,
            summary=summary,
        )
        self.url = url
        self.method = method
        self.multipart = multipart
        self.schema_requests = schema_requests

    @property
    def is_stream(self) -> bool:
        """Is the request we're preparing a stream, like an upload."""
        return any(request.is_stream_request for request in self.schema_requests)

    @property
    def operation_group_name(self) -> str:
        return self.yaml_data["language"]["python"]["operationGroupName"]

    def imports(self) -> FileImport:
        file_import = FileImport()
        for parameter in self.parameters:
            file_import.merge(parameter.imports())

        file_import.add_from_import(
            "azure.core.rest",
            "HttpRequest",
            ImportType.AZURECORE,
        )
        if self.parameters.path:
            file_import.add_from_import(
                "azure.core.pipeline.transport._base", "_format_url_section", ImportType.AZURECORE
            )
        file_import.add_from_import(
            "typing", "Any", ImportType.STDLIB, typing_section=TypingSection.CONDITIONAL
        )
        file_import.add_from_import("msrest", "Serializer", ImportType.AZURECORE)
        return file_import

    @staticmethod
    def get_parameter_converter() -> Callable:
        return RequestBuilderParameter.from_yaml

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any], *, code_model) -> "RequestBuilder":

        names = [
            "build",
            yaml_data["language"]["python"]["name"],
            "request"
        ]
        name = "_".join([n for n in names if n])

        first_request = yaml_data["requests"][0]

        parameters, multiple_media_type_parameters = (
            get_converted_parameters(yaml_data, cls.get_parameter_converter())
        )
        parameter_list = RequestBuilderParameterList(parameters + multiple_media_type_parameters)
        parameter_list.add_body_kwargs()

        request_builder_class = cls(
            yaml_data=yaml_data,
            name=name,
            url=first_request["protocol"]["http"]["path"],
            method=first_request["protocol"]["http"]["method"].upper(),
            multipart=first_request["protocol"]["http"].get("multipart", False),
            schema_requests=[SchemaRequest.from_yaml(yaml) for yaml in yaml_data["requests"]],
            parameters=parameter_list,
            description=yaml_data["language"]["python"]["description"],
            responses=[SchemaResponse.from_yaml(yaml) for yaml in yaml_data.get("responses", [])],
            summary=yaml_data["language"]["python"].get("summary"),
        )
        code_model.request_builder_ids[id(yaml_data)] = request_builder_class
        return request_builder_class
