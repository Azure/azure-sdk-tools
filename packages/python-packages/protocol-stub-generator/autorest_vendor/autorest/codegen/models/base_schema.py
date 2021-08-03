# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from abc import ABC, abstractmethod
from typing import Any, Dict, List, Optional, Union

from .base_model import BaseModel
from .imports import FileImport


class BaseSchema(BaseModel, ABC):
    """This is the base class for all schema models.

    :param yaml_data: the yaml data for this schema
    :type yaml_data: dict[str, Any]
    """

    def __init__(self, namespace: str, yaml_data: Dict[str, Any]) -> None:
        super().__init__(yaml_data)
        self.namespace = namespace
        self.default_value = yaml_data.get("defaultValue", None)
        self.xml_metadata = yaml_data.get("serialization", {}).get("xml", {})
        self.api_versions = set(value_dict["version"] for value_dict in yaml_data.get("apiVersions", []))

    @classmethod
    def from_yaml(
        cls, namespace: str, yaml_data: Dict[str, Any], **kwargs  # pylint: disable=unused-argument
    ) -> "BaseSchema":
        return cls(namespace=namespace, yaml_data=yaml_data)

    @property
    def has_xml_serialization_ctxt(self) -> bool:
        return bool(self.xml_metadata)

    def xml_serialization_ctxt(self) -> Optional[str]:
        """Return the serialization context in case this schema is used in an operation."""
        attrs_list = []
        if self.xml_metadata.get("name"):
            attrs_list.append(f"'name': '{self.xml_metadata['name']}'")
        if self.xml_metadata.get("attribute", False):
            attrs_list.append("'attr': True")
        if self.xml_metadata.get("prefix", False):
            attrs_list.append(f"'prefix': '{self.xml_metadata['prefix']}'")
        if self.xml_metadata.get("namespace", False):
            attrs_list.append(f"'ns': '{self.xml_metadata['namespace']}'")
        if self.xml_metadata.get("text"):
            attrs_list.append(f"'text': True")
        return ", ".join(attrs_list)

    def imports(self) -> FileImport:  # pylint: disable=no-self-use
        return FileImport()

    def model_file_imports(self) -> FileImport:
        return self.imports()

    @property
    @abstractmethod
    def serialization_type(self) -> str:
        """The tag recognized by 'msrest' as a serialization/deserialization.

        'str', 'int', 'float', 'bool' or
        https://github.com/Azure/msrest-for-python/blob/b505e3627b547bd8fdc38327e86c70bdb16df061/msrest/serialization.py#L407-L416

        or the object schema name (e.g. DotSalmon).

        If list: '[str]'
        If dict: '{str}'
        """
        ...

    @property
    @abstractmethod
    def docstring_text(self) -> str:
        """The names used in rtype documentation
        """
        ...

    @property
    @abstractmethod
    def docstring_type(self) -> str:
        """The python type used for RST syntax input.

        Special case for enum, for instance: 'str or ~namespace.EnumName'
        """
        ...

    @property
    def type_annotation(self) -> str:
        """The python type used for type annotation

        Special case for enum, for instance: Union[str, "EnumName"]
        """
        ...

    @property
    def operation_type_annotation(self) -> str:
        return self.type_annotation

    def get_declaration(self, value: Any) -> str:  # pylint: disable=no-self-use
        """Return the current value from YAML as a Python string that represents the constant.

        Example, if schema is "bytearray" and value is "foo",
        should return bytearray("foo", encoding="utf-8")
        as a string.

        This is important for constant serialization.

        By default, return value, since it works sometimes (integer)
        """
        return str(value)

    @property
    def default_value_declaration(self) -> str:
        """Return the default value as string using get_declaration.
        """
        if self.default_value is None:
            return "None"
        return self.get_declaration(self.default_value)

    @property
    def validation_map(self) -> Optional[Dict[str, Union[bool, int, str]]]:  # pylint: disable=no-self-use
        return None

    @property
    def serialization_constraints(self) -> Optional[List[str]]:  # pylint: disable=no-self-use
        return None

    @abstractmethod
    def get_json_template_representation(self, **kwargs: Any) -> Any:
        """Template of what this schema would look like as JSON input"""
        ...

    @abstractmethod
    def get_files_template_representation(self, **kwargs: Any) -> Any:
        """Template of what this schema would look like as files input"""
        ...
