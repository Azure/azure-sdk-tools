# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import logging
from typing import Dict, Any, Optional
from .base_schema import BaseSchema
from .primitive_schemas import get_primitive_schema, PrimitiveSchema
from .imports import FileImport

_LOGGER = logging.getLogger(__name__)


class ConstantSchema(BaseSchema):
    """Schema for constants that will be serialized.

    :param yaml_data: the yaml data for this schema
    :type yaml_data: dict[str, Any]
    :param str value: The actual value of this constant.
    :param schema: The schema for the value of this constant.
    :type schema: ~autorest.models.PrimitiveSchema
    """

    def __init__(
        self, namespace: str, yaml_data: Dict[str, Any], schema: PrimitiveSchema, value: Optional[str],
    ) -> None:
        super(ConstantSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
        self.value = value
        self.schema = schema

    def get_declaration(self, value: Any):
        if value != self.value:
            _LOGGER.warning(
                "Passed in value of %s differs from constant value of %s. Choosing constant value",
                str(value), str(self.value)
            )
        if self.value is None:
            return "None"
        return self.schema.get_declaration(self.value)

    @property
    def serialization_type(self) -> str:
        """Returns the serialization value for msrest.

        :return: The serialization value for msrest
        :rtype: str
        """
        return self.schema.serialization_type

    @property
    def docstring_text(self) -> str:
        return "constant"

    @property
    def docstring_type(self) -> str:
        """The python type used for RST syntax input and type annotation.

        :param str namespace: Optional. The namespace for the models.
        """
        return self.schema.docstring_type

    @property
    def type_annotation(self) -> str:
        return self.schema.type_annotation

    @classmethod
    def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> "ConstantSchema":
        """Constructs a ConstantSchema from yaml data.

        :param yaml_data: the yaml data from which we will construct this schema
        :type yaml_data: dict[str, Any]

        :return: A created ConstantSchema
        :rtype: ~autorest.models.ConstantSchema
        """
        name = yaml_data["language"]["python"]["name"] if yaml_data["language"]["python"].get("name") else ""
        _LOGGER.debug("Parsing %s constant", name)
        return cls(
            namespace=namespace,
            yaml_data=yaml_data,
            schema=get_primitive_schema(namespace=namespace, yaml_data=yaml_data["valueType"]),
            value=yaml_data.get("value", {}).get("value", None),
        )

    def get_json_template_representation(self, **kwargs: Any) -> Any:
        return self.schema.get_json_template_representation(**kwargs)

    def get_files_template_representation(self, **kwargs: Any) -> Any:
        return self.schema.get_files_template_representation(**kwargs)

    def imports(self) -> FileImport:
        file_import = FileImport()
        file_import.merge(self.schema.imports())
        return file_import
