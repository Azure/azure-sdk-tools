# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, List
from .base_model import BaseModel
from .request_builder import RequestBuilder
from .imports import FileImport

class Rest(BaseModel):
    """Everything that goes into the request_builders
    """
    def __init__(
        self,
        yaml_data: Dict[str, Any],
        request_builders: List[RequestBuilder]
    ):
        super(Rest, self). __init__(yaml_data=yaml_data)
        self.request_builders = request_builders

    def imports(self) -> FileImport:
        file_import = FileImport()
        for request_builder in self.request_builders:
            file_import.merge(request_builder.imports())
        return file_import

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any], *, code_model) -> "Rest":
        request_builders = []
        if yaml_data.get("operationGroups"):
            request_builders = [
                RequestBuilder.from_yaml(operation_yaml, code_model=code_model)
                for og_group in yaml_data["operationGroups"]
                for operation_yaml in og_group["operations"]
            ]

        return cls(
            yaml_data=yaml_data,
            request_builders=request_builders
        )
