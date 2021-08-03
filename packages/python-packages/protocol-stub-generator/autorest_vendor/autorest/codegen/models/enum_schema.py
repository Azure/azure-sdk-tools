# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, List, Optional, Set
from .base_schema import BaseSchema
from .primitive_schemas import PrimitiveSchema, get_primitive_schema, StringSchema
from .imports import FileImport, ImportType, TypingSection


class EnumValue:
    """Model containing necessary information for a single value of an enum.

    :param str name: The name of this enum value
    :param str value: The value of this enum value
    :param str description: Optional. The description for this enum value
    """

    def __init__(self, name: str, value: str, description: Optional[str] = None) -> None:
        self.name = name
        self.value = value
        self.description = description

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]) -> "EnumValue":
        """Constructs an EnumValue from yaml data.

        :param yaml_data: the yaml data from which we will construct this object
        :type yaml_data: dict[str, Any]

        :return: A created EnumValue
        :rtype: ~autorest.models.EnumValue
        """
        return cls(
            name=yaml_data["language"]["python"]["name"],
            value=yaml_data["value"],
            description=yaml_data["language"]["python"].get("description"),
        )


class EnumSchema(BaseSchema):
    """Schema for enums that will be serialized.

    :param yaml_data: the yaml data for this schema
    :type yaml_data: dict[str, Any]
    :param str description: The description of this enum
    :param str name: The name of the enum.
    :type element_type: ~autorest.models.PrimitiveSchema
    :param values: List of the values for this enum
    :type values: list[~autorest.models.EnumValue]
    """

    def __init__(
        self,
        namespace: str,
        yaml_data: Dict[str, Any],
        description: str,
        name: str,
        values: List["EnumValue"],
        enum_type: PrimitiveSchema,
        enum_file_name: str
    ) -> None:
        super(EnumSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
        self.description = description
        self.name = name
        self.values = values
        self.enum_file_name = enum_file_name
        self.enum_type = enum_type

    def __lt__(self, other):
        return self.name.lower() < other.name.lower()

    @property
    def serialization_type(self) -> str:
        """Returns the serialization value for msrest.

        :return: The serialization value for msrest
        :rtype: str
        """
        return self.enum_type.serialization_type

    @property
    def type_annotation(self) -> str:
        """The python type used for type annotation

        :return: The type annotation for this schema
        :rtype: str
        """
        return f'Union[{self.enum_type.type_annotation}, "{self.name}"]'

    @property
    def operation_type_annotation(self) -> str:
        """The python type used for type annotation

        :return: The type annotation for this schema
        :rtype: str
        """
        return f'Union[{self.enum_type.type_annotation}, "_models.{self.name}"]'

    def get_declaration(self, value: Any) -> str:
        return f'"{value}"'

    @property
    def docstring_text(self) -> str:
        return self.name

    @property
    def docstring_type(self) -> str:
        """The python type used for RST syntax input and type annotation.
        """
        return f"str or ~{self.namespace}.models.{self.name}"

    @staticmethod
    def _get_enum_values(yaml_data: List[Dict[str, Any]]) -> List["EnumValue"]:
        """Creates the list of values for this enum.

        :param yaml_data: yaml data about the enum's values
        :type yaml_data: dict[str, Any]
        :return: The list of values for this enum
        :rtype: list[~autorest.models.EnumValue]
        """
        values = []
        seen_enums: Set[str] = set()

        for enum in yaml_data:
            enum_name = enum["language"]["python"]["name"]
            if enum_name in seen_enums:
                continue
            values.append(EnumValue.from_yaml(enum))
            seen_enums.add(enum_name)
        return values

    def get_json_template_representation(self, **kwargs: Any) -> Any:
        return self.enum_type.get_json_template_representation(**kwargs)

    def get_files_template_representation(self, **kwargs: Any) -> Any:
        return self.enum_type.get_files_template_representation(**kwargs)

    @classmethod
    def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs: Any) -> "EnumSchema":
        """Constructs an EnumSchema from yaml data.

        :param yaml_data: the yaml data from which we will construct this schema
        :type yaml_data: dict[str, Any]

        :return: A created EnumSchema
        :rtype: ~autorest.models.EnumSchema
        """
        name = yaml_data["language"]["python"]["name"]

        # choice type doesn't always exist. if there is no choiceType, we default to string
        if yaml_data.get("choiceType"):
            enum_type = get_primitive_schema(namespace, yaml_data["choiceType"])
        else:
            enum_type = StringSchema(namespace, {"type": "str"})
        values = EnumSchema._get_enum_values(yaml_data["choices"])
        code_model = kwargs.pop("code_model")

        return cls(
            namespace=namespace,
            yaml_data=yaml_data,
            description=yaml_data["language"]["python"]["description"],
            name=name,
            values=values,
            enum_type=enum_type,
            enum_file_name=f"_{code_model.module_name}_enums"
        )

    def imports(self) -> FileImport:
        file_import = FileImport()
        file_import.add_from_import("typing", "Union", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.merge(self.enum_type.imports())
        return file_import

    def model_file_imports(self) -> FileImport:
        imports = self.imports()
        # we import every enum since we can get extremely long imports
        # if we import my name
        imports.add_from_import("." + self.enum_file_name, "*", ImportType.LOCAL)
        return imports
