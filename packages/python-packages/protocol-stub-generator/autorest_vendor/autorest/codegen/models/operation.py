# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from itertools import chain
import logging
from typing import Callable, cast, Dict, List, Any, Optional, Union, Set

from .base_builder import BaseBuilder, get_converted_parameters
from .imports import FileImport, ImportType, TypingSection
from .schema_response import SchemaResponse
from .parameter import Parameter
from .parameter_list import ParameterList
from .base_schema import BaseSchema
from .object_schema import ObjectSchema
from .request_builder import RequestBuilder

_LOGGER = logging.getLogger(__name__)

class Operation(BaseBuilder):  # pylint: disable=too-many-public-methods, too-many-instance-attributes
    """Represent an self.
    """

    def __init__(
        self,
        yaml_data: Dict[str, Any],
        name: str,
        description: str,
        api_versions: Set[str],
        parameters: ParameterList,
        multiple_media_type_parameters: ParameterList,
        summary: Optional[str] = None,
        responses: Optional[List[SchemaResponse]] = None,
        exceptions: Optional[List[SchemaResponse]] = None,
        want_description_docstring: bool = True,
        want_tracing: bool = True,
    ) -> None:
        super().__init__(
            yaml_data=yaml_data,
            name=name,
            description=description,
            parameters=parameters,
            responses=responses,
            summary=summary,
        )
        self.multiple_media_type_parameters = multiple_media_type_parameters
        self.api_versions = api_versions
        self.multiple_media_type_parameters = multiple_media_type_parameters
        self.exceptions = exceptions or []
        self.want_description_docstring = want_description_docstring
        self.want_tracing = want_tracing
        self._request_builder: Optional[RequestBuilder] = None
        self.deprecated = False

    @property
    def python_name(self) -> str:
        return self.name

    @property
    def request_builder(self) -> RequestBuilder:
        if not self._request_builder:
            raise ValueError(
                "You're calling request_builder when you haven't linked up operation to its "
                "request builder through the code model"
            )
        return self._request_builder

    @request_builder.setter
    def request_builder(self, r: RequestBuilder) -> None:
        self._request_builder = r

    @property
    def is_stream_response(self) -> bool:
        """Is the response expected to be streamable, like a download."""
        return any(response.is_stream_response for response in self.responses)

    @property
    def body_kwargs_to_pass_to_request_builder(self) -> List[str]:
        kwargs = []
        if self.request_builder.multipart:
            kwargs.append("files")
        if self.parameters.has_partial_body:
            kwargs.append("data")
        if any([ct for ct in self.parameters.content_types if "json" in ct]):
            kwargs.append("json")
        if self.request_builder.is_stream or not kwargs:
            kwargs.append("content")
        return kwargs

    @property
    def serialized_body_kwarg(self) -> str:
        # body serialization can be passed to either "json" or "content"
        if "json" in self.body_kwargs_to_pass_to_request_builder:
            return "json"
        if not self.request_builder.is_stream:
            return "content"
        raise ValueError("You should not be trying to serialize this body")

    @property
    def has_optional_return_type(self) -> bool:
        """Has optional return type if there are multiple successful response types where some have
        bodies and some are None
        """

        # successful status codes of responses that have bodies
        status_codes_for_responses_with_bodies = [
            code for code in self.success_status_code
            if isinstance(code, int) and self.get_response_from_status(code).has_body
        ]

        successful_responses = [
            response for response in self.responses
            if any(code in self.success_status_code for code in response.status_codes)
        ]

        return (
            self.has_response_body and
            len(successful_responses) > 1 and
            len(self.success_status_code) != len(status_codes_for_responses_with_bodies)
        )

    @property
    def serialization_context(self) -> str:
        # FIXME Do the serialization context (XML)
        return ""

    @property
    def has_response_body(self) -> bool:
        """Tell if at least one response has a body.
        """
        return any(response.has_body or response.is_stream_response for response in self.responses)

    @property
    def any_response_has_headers(self) -> bool:
        return any(response.has_headers for response in self.responses)

    @property
    def default_exception(self) -> Optional[str]:
        default_excp = [excp for excp in self.exceptions for code in excp.status_codes if code == "default"]
        if not default_excp:
            return None
        excep_schema = default_excp[0].schema
        if isinstance(excep_schema, ObjectSchema):
            return f"_models.{excep_schema.name}"
        # in this case, it's just an AnySchema
        return "\'object\'"

    @property
    def status_code_exceptions(self) -> List[SchemaResponse]:
        return [excp for excp in self.exceptions if list(excp.status_codes) != ["default"]]

    @property
    def status_code_exceptions_status_codes(self) -> List[Union[str, int]]:
        """Actually returns all of the status codes from exceptions (besides default)"""
        return list(chain.from_iterable([
            excp.status_codes for excp in self.status_code_exceptions
        ]))

    def _imports_shared(self) -> FileImport:
        file_import = FileImport()
        file_import.add_from_import("typing", "Any", ImportType.STDLIB, TypingSection.CONDITIONAL)
        for param in self.parameters.method:
            file_import.merge(param.imports())

        for param in self.multiple_media_type_parameters:
            file_import.merge(param.imports())

        for response in [r for r in self.responses if r.has_body]:
            file_import.merge(cast(BaseSchema, response.schema).imports())

        if len([r for r in self.responses if r.has_body]) > 1:
            file_import.add_from_import("typing", "Union", ImportType.STDLIB, TypingSection.CONDITIONAL)

        if self.is_stream_response:
            file_import.add_from_import("typing", "IO", ImportType.STDLIB, TypingSection.CONDITIONAL)
        return file_import


    def imports_for_multiapi(self, code_model, async_mode: bool) -> FileImport:  # pylint: disable=unused-argument
        return self._imports_shared()

    def imports(self, code_model, async_mode: bool) -> FileImport:
        file_import = self._imports_shared()

        # Exceptions
        file_import.add_from_import("azure.core.exceptions", "map_error", ImportType.AZURECORE)
        if code_model.options["azure_arm"]:
            file_import.add_from_import("azure.mgmt.core.exceptions", "ARMErrorFormat", ImportType.AZURECORE)
        file_import.add_from_import("azure.core.exceptions", "HttpResponseError", ImportType.AZURECORE)


        file_import.add_import("functools", ImportType.STDLIB)
        file_import.add_from_import("typing", "Callable", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.add_from_import("typing", "Optional", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.add_from_import("typing", "Dict", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.add_from_import("typing", "TypeVar", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.add_from_import("typing", "Generic", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.add_from_import("azure.core.pipeline", "PipelineResponse", ImportType.AZURECORE)
        file_import.add_from_import("azure.core.rest", "HttpRequest", ImportType.AZURECORE)
        if async_mode:
            file_import.add_from_import("azure.core.pipeline.transport", "AsyncHttpResponse", ImportType.AZURECORE)
        else:
            file_import.add_from_import("azure.core.pipeline.transport", "HttpResponse", ImportType.AZURECORE)

        # Deprecation
        # FIXME: Replace with "the YAML contains deprecated:true"
        if True:  # pylint: disable=using-constant-test
            file_import.add_import("warnings", ImportType.STDLIB)

        operation_group_name = self.request_builder.operation_group_name
        rest_import_path = "..." if async_mode else ".."
        if operation_group_name:
            file_import.add_from_import(
                f"{rest_import_path}{code_model.rest_layer_name}",
                name_import=operation_group_name,
                import_type=ImportType.LOCAL,
                alias=f"rest_{operation_group_name}"
            )
        else:
            file_import.add_from_import(
                rest_import_path,
                code_model.rest_layer_name,
                import_type=ImportType.LOCAL,
                alias="rest"
            )
        return file_import

    def convert_multiple_media_type_parameters(self) -> None:
        type_annot = ", ".join([
            param.schema.operation_type_annotation
            for param in self.multiple_media_type_parameters
        ])
        docstring_type = " or ".join([
            param.schema.docstring_type for param in self.multiple_media_type_parameters
        ])
        try:
            # get an optional param with object first. These params are the top choice
            # bc they have more info about how to serialize the body
            chosen_parameter = next(
                p for p in self.multiple_media_type_parameters if not p.required and isinstance(p.schema, ObjectSchema)
            )
        except StopIteration:  # pylint: disable=broad-except
            # otherwise, we get the first optional param, if that exists. If not, we just grab the first one
            optional_parameters = [p for p in self.multiple_media_type_parameters if not p.required]
            chosen_parameter = optional_parameters[0] if optional_parameters else self.multiple_media_type_parameters[0]
        if not chosen_parameter:
            raise ValueError("You are missing a parameter that has multiple media types")
        chosen_parameter.multiple_media_types_type_annot = f"Union[{type_annot}]"
        chosen_parameter.multiple_media_types_docstring_type = docstring_type
        self.parameters.append(chosen_parameter)

    @staticmethod
    def get_parameter_converter() -> Callable:
        return Parameter.from_yaml

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]) -> "Operation":
        name = yaml_data["language"]["python"]["name"]
        _LOGGER.debug("Parsing %s operation", name)

        parameters, multiple_media_type_parameters = get_converted_parameters(yaml_data, cls.get_parameter_converter())

        return cls(
            yaml_data=yaml_data,
            name=name,
            description=yaml_data["language"]["python"]["description"],
            api_versions=set(value_dict["version"] for value_dict in yaml_data["apiVersions"]),
            parameters=ParameterList(parameters),
            multiple_media_type_parameters=ParameterList(multiple_media_type_parameters),
            summary=yaml_data["language"]["python"].get("summary"),
            responses=[SchemaResponse.from_yaml(yaml) for yaml in yaml_data.get("responses", [])],
            # Exception with no schema means default exception, we don't store them
            exceptions=[SchemaResponse.from_yaml(yaml) for yaml in yaml_data.get("exceptions", []) if "schema" in yaml],
        )
