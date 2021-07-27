# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import logging
from typing import cast, Dict, List, Any, Optional, Set

from .operation import Operation
from .schema_response import SchemaResponse
from .request_builder import RequestBuilder
from .imports import ImportType, FileImport, TypingSection
from .object_schema import ObjectSchema
from .parameter_list import ParameterList

_LOGGER = logging.getLogger(__name__)


class PagingOperation(Operation):
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
        *,
        override_success_response_to_200: bool = False
    ) -> None:
        super(PagingOperation, self).__init__(
            yaml_data,
            name,
            description,
            api_versions,
            parameters,
            multiple_media_type_parameters,
            summary,
            responses,
            exceptions,
            want_description_docstring,
            want_tracing
        )
        self._item_name: str = yaml_data["extensions"]["x-ms-pageable"].get("itemName")
        self._next_link_name: str = yaml_data["extensions"]["x-ms-pageable"].get("nextLinkName")
        self.operation_name: str = yaml_data["extensions"]["x-ms-pageable"].get("operationName")
        self.next_operation: Optional[Operation] = None
        self.override_success_response_to_200 = override_success_response_to_200

    def _get_response(self) -> SchemaResponse:
        response = self.responses[0]
        if not isinstance(response.schema, ObjectSchema):
            raise ValueError(
                "The response of a paging operation must be of type " + f"ObjectSchema but {response.schema} is not"
            )
        return response

    def _find_python_name(self, rest_api_name: str, log_name: str) -> str:
        response = self._get_response()
        response_schema = cast(ObjectSchema, response.schema)
        if response_schema:
            for prop in response_schema.properties:
                if prop.original_swagger_name == rest_api_name:
                    return prop.name
        raise ValueError(
            f"While scanning x-ms-pageable, was unable to find "
            + f"{log_name}:{rest_api_name} in model {response_schema.name}"
        )

    def _get_paging_extension(self, extension_name):
        return self.yaml_data["extensions"][extension_name]

    @property
    def item_name(self) -> str:
        if self._item_name is None:
            # Default value. I still check if I find it,  so I can do a nice message.
            item_name = "value"
            try:
                return self._find_python_name(item_name, "itemName")
            except ValueError:
                response = self._get_response()
                raise ValueError(
                    f"While scanning x-ms-pageable, itemName was not defined and object"
                    + f" {response.schema.name} has no array called 'value'"
                )
        return self._find_python_name(self._item_name, "itemName")

    @property
    def next_link_name(self) -> Optional[str]:
        if not self._next_link_name:
            # That's an ok scenario, it just means no next page possible
            return None
        return self._find_python_name(self._next_link_name, "nextLinkName")

    @property
    def has_optional_return_type(self) -> bool:
        """A paging will never have an optional return type, we will always return a pager"""
        return False

    def get_pager_path(self, async_mode: bool) -> str:
        extension_name = "pager-async" if async_mode else "pager-sync"
        return self._get_paging_extension(extension_name)

    def get_pager(self, async_mode: bool) -> str:
        return self.get_pager_path(async_mode).split(".")[-1]

    @property
    def next_request_builder(self) -> Optional[RequestBuilder]:
        if not self.next_operation:
            return None
        next_request_builder = self.next_operation.request_builder
        return next_request_builder

    def _imports_shared(self, async_mode: bool) -> FileImport:
        file_import = super()._imports_shared(async_mode)
        if async_mode:
            file_import.add_from_import("typing", "AsyncIterable", ImportType.STDLIB, TypingSection.CONDITIONAL)
        else:
            file_import.add_from_import("typing", "Iterable", ImportType.STDLIB, TypingSection.CONDITIONAL)
        if self.next_request_builder:
            file_import.merge(self.next_request_builder.imports())
        return file_import

    def imports_for_multiapi(self, code_model, async_mode: bool) -> FileImport:
        file_import = super().imports_for_multiapi(code_model, async_mode)
        pager_import_path = ".".join(self.get_pager_path(async_mode).split(".")[:-1])
        pager = self.get_pager(async_mode)

        file_import.add_from_import(pager_import_path, pager, ImportType.AZURECORE, TypingSection.CONDITIONAL)

        return file_import

    def imports(self, code_model, async_mode: bool) -> FileImport:
        file_import = super(PagingOperation, self).imports(code_model, async_mode)

        pager_import_path = ".".join(self.get_pager_path(async_mode).split(".")[:-1])
        pager = self.get_pager(async_mode)

        file_import.add_from_import(pager_import_path, pager, ImportType.AZURECORE)

        if async_mode:
            file_import.add_from_import("azure.core.async_paging", "AsyncList", ImportType.AZURECORE)

        if code_model.options["tracing"]:
            file_import.add_from_import(
                "azure.core.tracing.decorator", "distributed_trace", ImportType.AZURECORE,
            )

        return file_import
