# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, Optional
from .parameter import ParameterOnlyPathsPositional, ParameterLocation, ParameterStyle
from .constant_schema import ConstantSchema

def _make_public(name):
    if name[0] == "_":
        return name[1:]
    return name

class RequestBuilderParameter(ParameterOnlyPathsPositional):

    @staticmethod
    def serialize_line(function_name: str, parameters_line: str):
        return f'_SERIALIZER.{function_name}({parameters_line})'

    @property
    def in_method_signature(self) -> bool:
        return not(
            # constant bodies still go in method signature bc we don't support that in our request builder
            (self.constant and not self.location == ParameterLocation.Body)
            # If i'm not in the method code, no point in being in signature
            or not self.in_method_code
            # If I'm a flattened property of a body, don't want me, want the body param
            or self.target_property_name
            or not self.in_method_code
        )

    @property
    def name_in_high_level_operation(self) -> str:
        if self.is_body:
            if self.is_multipart:
                return "files"
            if self.is_partial_body:
                return "data"
            return "json"
        name = self.yaml_data["language"]["python"]["name"]
        if self.implementation == "Client" and self.in_method_code:
            # for these, we're passing the client params to the request builder.
            # Need the self._config prefix
            name = f"self._config.{name}"
        return name

    @property
    def in_method_code(self) -> bool:
        if isinstance(self.schema, ConstantSchema) and self.location == ParameterLocation.Body:
            # constant bodies aren't really a thing in requests
            # users need to explicitly pass the constant body through the method signature
            return True
        if self.location == ParameterLocation.Uri:
            # don't want any base url path formatting arguments
            return False
        return super(RequestBuilderParameter, self).in_method_code

    @property
    def default_value(self) -> Optional[Any]:
        if self.location == ParameterLocation.Body:
            return None
        return super(RequestBuilderParameter, self).default_value

    @property
    def default_value_declaration(self) -> Optional[str]:
        if self.serialized_name == "content_type":
            # in request builders we're in a weird scenario
            # we need to know what the types of content_type are
            # but we want to serialize content_type with no default.
            # So, we just return None in default_value_declaration for now
            return "None"
        return super().default_value_declaration

    @property
    def is_keyword_only(self) -> bool:
        return not self.location == ParameterLocation.Path and not self.is_kwarg

    @property
    def full_serialized_name(self) -> str:
        return self.serialized_name

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]) -> "RequestBuilderParameter":
        http_protocol = yaml_data["protocol"].get("http", {"in": ParameterLocation.Other})
        name = yaml_data["language"]["python"]["name"]
        location = ParameterLocation(http_protocol["in"])
        return cls(
            yaml_data=yaml_data,
            schema=yaml_data.get("schema", None),  # FIXME replace by operation model
            # See also https://github.com/Azure/autorest.modelerfour/issues/80
            rest_api_name=yaml_data["language"]["default"].get(
                "serializedName", yaml_data["language"]["default"]["name"]
            ),
            serialized_name=_make_public(name),
            description=yaml_data["language"]["python"]["description"],
            implementation=yaml_data["implementation"],
            required=yaml_data.get("required", False),
            location=location,
            skip_url_encoding=yaml_data.get("extensions", {}).get("x-ms-skip-url-encoding", False),
            constraints=[],  # FIXME constraints
            target_property_name=id(yaml_data["targetProperty"]) if yaml_data.get("targetProperty") else None,
            style=ParameterStyle(http_protocol["style"]) if "style" in http_protocol else None,
            explode=http_protocol.get("explode", False),
            grouped_by=yaml_data.get("groupedBy", None),
            original_parameter=yaml_data.get("originalParameter", None),
            flattened=yaml_data.get("flattened", False),
            client_default_value=yaml_data.get("clientDefaultValue"),
        )
