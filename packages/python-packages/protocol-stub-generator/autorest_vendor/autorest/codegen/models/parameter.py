# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import logging
from enum import Enum

from typing import Dict, Optional, List, Any, Union, Tuple, cast

from .imports import FileImport, ImportType, TypingSection
from .base_model import BaseModel
from .base_schema import BaseSchema
from .list_schema import ListSchema
from .constant_schema import ConstantSchema
from .object_schema import ObjectSchema
from .property import Property


_LOGGER = logging.getLogger(__name__)

_HIDDEN_KWARGS = ["content_type"]


class ParameterLocation(Enum):
    Path = "path"
    Body = "body"
    Query = "query"
    Header = "header"
    Uri = "uri"
    Other = "other"


class ParameterStyle(Enum):
    simple = "simple"
    label = "label"
    matrix = "matrix"
    form = "form"
    spaceDelimited = "spaceDelimited"
    pipeDelimited = "pipeDelimited"
    deepObject = "deepObject"
    tabDelimited = "tabDelimited"
    json = "json"
    binary = "binary"
    xml = "xml"
    multipart = "multipart"


class Parameter(BaseModel):  # pylint: disable=too-many-instance-attributes, too-many-public-methods
    def __init__(
        self,
        yaml_data: Dict[str, Any],
        schema: BaseSchema,
        rest_api_name: str,
        serialized_name: str,
        description: str,
        implementation: str,
        required: bool,
        location: ParameterLocation,
        skip_url_encoding: bool,
        constraints: List[Any],
        target_property_name: Optional[Union[int, str]] = None,  # first uses id as placeholder
        style: Optional[ParameterStyle] = None,
        explode: Optional[bool] = False,
        *,
        flattened: bool = False,
        grouped_by: Optional["Parameter"] = None,
        original_parameter: Optional["Parameter"] = None,
        client_default_value: Optional[Any] = None,
    ) -> None:
        super().__init__(yaml_data)
        self.schema = schema
        self.rest_api_name = rest_api_name
        self.serialized_name = serialized_name
        self.description = description
        self._implementation = implementation
        self.required = required
        self.location = location
        self.skip_url_encoding = skip_url_encoding
        self.constraints = constraints
        self.target_property_name = target_property_name
        self.style = style
        self.explode = explode
        self.flattened = flattened
        self.grouped_by = grouped_by
        self.original_parameter = original_parameter
        self.client_default_value = client_default_value
        self.has_multiple_media_types: bool = False
        self.multiple_media_types_type_annot: Optional[str] = None
        self.multiple_media_types_docstring_type: Optional[str] = None
        self.is_partial_body = yaml_data.get("isPartialBody", False)

    def __hash__(self) -> int:
        return hash(self.serialized_name)

    def serialize_line(self, function_name: str, parameters_line: str):  # pylint: disable=no-self-use
        return f'self._serialize.{function_name}({parameters_line})'

    def build_serialize_data_call(self, function_name: str) -> str:

        optional_parameters = []

        if self.skip_url_encoding:
            optional_parameters.append("skip_quote=True")

        if self.style and not self.explode:
            if self.style in [ParameterStyle.simple, ParameterStyle.form]:
                div_char = ","
            elif self.style in [ParameterStyle.spaceDelimited]:
                div_char = " "
            elif self.style in [ParameterStyle.pipeDelimited]:
                div_char = "|"
            elif self.style in [ParameterStyle.tabDelimited]:
                div_char = "\t"
            else:
                raise ValueError(f"Do not support {self.style} yet")
            optional_parameters.append(f"div='{div_char}'")

        if self.explode:
            if not isinstance(self.schema, ListSchema):
                raise ValueError("Got a explode boolean on a non-array schema")
            serialization_schema = self.schema.element_type
        else:
            serialization_schema = self.schema

        serialization_constraints = serialization_schema.serialization_constraints
        if serialization_constraints:
            optional_parameters += serialization_constraints

        origin_name = self.full_serialized_name

        parameters = [
            f'"{origin_name.lstrip("_")}"',
            "q" if self.explode else origin_name,
            f"'{serialization_schema.serialization_type}'",
            *optional_parameters
        ]
        parameters_line = ', '.join(parameters)

        serialize_line = self.serialize_line(function_name, parameters_line)

        if self.explode:
            return f"[{serialize_line} if q is not None else '' for q in {origin_name}]"
        return serialize_line

    @property
    def constant(self) -> bool:
        """Returns whether a parameter is a constant or not.
        Checking to see if it's required, because if not, we don't consider it
        a constant because it can have a value of None.
        """
        if not isinstance(self.schema, ConstantSchema):
            return False
        return self.required

    @property
    def is_multipart(self) -> bool:
        return self.yaml_data["language"]["python"].get("multipart", False)

    @property
    def constant_declaration(self) -> str:
        if self.schema:
            if isinstance(self.schema, ConstantSchema):
                return self.schema.get_declaration(self.schema.value)
            raise ValueError(
                "Trying to get constant declaration for a schema that is not ConstantSchema"
                )
        raise ValueError("Trying to get a declaration for a schema that doesn't exist")

    @property
    def xml_serialization_ctxt(self) -> str:
        return self.schema.xml_serialization_ctxt() or ""

    @property
    def is_body(self) -> bool:
        return self.location == ParameterLocation.Body

    @property
    def in_method_signature(self) -> bool:
        return not(
            # If I only have one value, I can't be set, so no point being in signature
            self.constant
            # If i'm not in the method code, no point in being in signature
            or not self.in_method_code
            # If I'm grouped, my grouper will be on signature, not me
            or self.grouped_by
            # If I'm body and it's flattened, I'm not either
            or (self.is_body and self.flattened)
        )

    @property
    def corresponding_grouped_property(self) -> Property:
        if not self.grouped_by:
            raise ValueError("Should only be calling if your parameter is grouped")
        try:
            return next(
                p for p in cast(ObjectSchema, self.grouped_by.schema).properties
                if any(op for op in p.yaml_data['originalParameter'] if id(op) == self.id)
            )
        except StopIteration:
            raise ValueError("There is not a corresponding grouped property for your parameter.")

    @property
    def in_method_code(self) -> bool:
        return not (
            self.constant and
            self.location == ParameterLocation.Other or
            self.rest_api_name == '$host'
        )

    @property
    def implementation(self) -> str:
        # https://github.com/Azure/autorest.modelerfour/issues/81
        if self.serialized_name == "api_version":
            return "Method"
        return self._implementation

    def _default_value(self) -> Tuple[Optional[Any], str, str]:
        type_annot = self.multiple_media_types_type_annot or self.schema.operation_type_annotation
        if not self.required and not type_annot == "Any":
            type_annot = f"Optional[{type_annot}]"

        if self.client_default_value is not None:
            return self.client_default_value, self.schema.get_declaration(self.client_default_value), type_annot

        if self.multiple_media_types_type_annot:
            # means this parameter has multiple media types. We force default value to be None.
            default_value = None
            default_value_declaration = "None"
        else:
            if isinstance(self.schema, ConstantSchema):
                default_value = self.schema.get_declaration(self.schema.value)
                default_value_declaration = default_value
            else:
                default_value = self.schema.default_value
                default_value_declaration = self.schema.default_value_declaration
        if default_value is not None and self.required:
            _LOGGER.warning(
                "Parameter '%s' is required and has a default value, this combination is not recommended",
                self.rest_api_name
            )

        return default_value, default_value_declaration, type_annot

    @property
    def description_keyword(self) -> str:
        return "keyword" if self.is_kwarg or self.is_keyword_only else "param"

    @property
    def docstring_type_keyword(self) -> str:
        return "paramtype" if self.is_kwarg or self.is_keyword_only else "type"

    @property
    def default_value(self) -> Optional[Any]:
        # exposing default_value because client_default_value doesn't get updated with
        # default values we bubble up from the schema
        return self._default_value()[0]

    @property
    def default_value_declaration(self) -> Optional[Any]:
        return self._default_value()[1]

    @property
    def type_annotation(self) -> str:
        return self._default_value()[2]

    @property
    def serialization_type(self) -> str:
        return self.schema.serialization_type

    @property
    def docstring_type(self) -> str:
        return self.multiple_media_types_docstring_type or self.schema.docstring_type

    @property
    def has_default_value(self):
        return self.default_value is not None or not self.required

    def method_signature(self, async_mode: bool) -> str:
        if async_mode:
            if self.has_default_value:
                return f"{self.serialized_name}: {self.type_annotation} = {self.default_value_declaration},"
            return f"{self.serialized_name}: {self.type_annotation},"
        if self.has_default_value:
            return f"{self.serialized_name}={self.default_value_declaration},  # type: {self.type_annotation}"
        return f"{self.serialized_name},  # type: {self.type_annotation}"

    @property
    def full_serialized_name(self) -> str:
        origin_name = self.serialized_name
        if self.implementation == "Client":
            origin_name = f"self._config.{self.serialized_name}"
        return origin_name

    @property
    def is_kwarg(self) -> bool:
        # this means "am I in **kwargs?"
        return self.rest_api_name == "Content-Type"

    @property
    def is_keyword_only(self) -> bool:
        # this means in async mode, I am documented like def hello(positional_1, *, me!)
        return False

    @property
    def is_hidden(self) -> bool:
        return self.serialized_name in _HIDDEN_KWARGS

    @property
    def is_positional(self) -> bool:
        return self.in_method_signature and not (self.is_keyword_only or self.is_kwarg)

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]) -> "Parameter":
        http_protocol = yaml_data["protocol"].get("http", {"in": ParameterLocation.Other})
        return cls(
            yaml_data=yaml_data,
            schema=yaml_data.get("schema", None),  # FIXME replace by operation model
            # See also https://github.com/Azure/autorest.modelerfour/issues/80
            rest_api_name=yaml_data["language"]["default"].get(
                "serializedName", yaml_data["language"]["default"]["name"]
            ),
            serialized_name=yaml_data["language"]["python"]["name"],
            description=yaml_data["language"]["python"]["description"],
            implementation=yaml_data["implementation"],
            required=yaml_data.get("required", False),
            location=ParameterLocation(http_protocol["in"]),
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

    def imports(self) -> FileImport:
        file_import = self.schema.imports()
        if not self.required:
            file_import.add_from_import("typing", "Optional", ImportType.STDLIB, TypingSection.CONDITIONAL)
        if self.has_multiple_media_types:
            file_import.add_from_import("typing", "Union", ImportType.STDLIB, TypingSection.CONDITIONAL)
        return file_import

class ParameterOnlyPathsPositional(Parameter):

    @property
    def is_keyword_only(self) -> bool:
        return not (self.location == ParameterLocation.Path or self.location == ParameterLocation.Body)
