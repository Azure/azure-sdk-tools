# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Dict, Optional, List, Union, Any, cast

from .base_model import BaseModel
from .base_schema import BaseSchema
from .object_schema import ObjectSchema


class HeaderResponse:
    def __init__(self, name: str, schema) -> None:
        self.name = name
        self.schema = schema

    @property
    def serialization_type(self) -> str:
        return self.schema.serialization_type


class SchemaResponse(BaseModel):
    def __init__(
        self,
        yaml_data: Dict[str, Any],
        schema: Optional[BaseSchema],
        media_types: List[str],
        status_codes: List[Union[str, int]],
        headers: List[HeaderResponse],
        binary: bool,
    ) -> None:
        super().__init__(yaml_data)
        self.schema = schema
        self.media_types = media_types
        self.status_codes = status_codes
        self.headers = headers
        self.binary = binary
        self.nullable = self.yaml_data.get("nullable", False)

    @property
    def has_body(self) -> bool:
        """Tell if that response defines a body.
        """
        return bool(self.schema)

    @property
    def has_headers(self) -> bool:
        """Tell if that response defines headers.
        """
        return bool(self.headers)

    @property
    def serialization_type(self) -> str:
        if self.schema:
            return self.schema.serialization_type
        return "None"

    @property
    def operation_type_annotation(self) -> str:
        if not self.schema:
            return "None"
        if self.nullable:
            return f"Optional[{self.schema.operation_type_annotation}]"
        return self.schema.operation_type_annotation

    @property
    def docstring_text(self) -> str:
        if not self.schema:
            return "None"
        if self.nullable:
            return f"{self.schema.docstring_text} or None"
        return self.schema.docstring_text

    @property
    def docstring_type(self) -> str:
        if not self.schema:
            return "None"
        if self.nullable:
            return f"{self.schema.docstring_type} or None"
        return self.schema.docstring_type

    @property
    def is_stream_response(self) -> bool:
        """Is the response expected to be streamable, like a download."""
        return self.binary

    @property
    def is_exception(self) -> bool:
        if self.schema:
            return cast(ObjectSchema, self.schema).is_exception
        return False

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]) -> "SchemaResponse":

        return cls(
            yaml_data=yaml_data,
            schema=yaml_data.get("schema", None),  # FIXME replace by operation model
            media_types=yaml_data["protocol"]["http"].get("mediaTypes", []),
            status_codes=[
                int(code) if code != "default" else "default" for code in yaml_data["protocol"]["http"]["statusCodes"]
            ],
            headers=[
                HeaderResponse(header_prop["header"], header_prop["schema"])
                for header_prop in yaml_data["protocol"]["http"].get("headers", [])
            ],
            binary=yaml_data.get("binary", False),
        )

    def __repr__(self) -> str:
        return f"<{self.__class__.__name__} {self.status_codes}>"
