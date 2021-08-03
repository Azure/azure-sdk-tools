# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, Optional, Union
from .base_schema import BaseSchema
from .imports import FileImport, ImportType, TypingSection


class ListSchema(BaseSchema):
    def __init__(
        self,
        namespace: str,
        yaml_data: Dict[str, Any],
        element_type: BaseSchema,
        *,
        max_items: Optional[int] = None,
        min_items: Optional[int] = None,
        unique_items: Optional[int] = None,
    ) -> None:
        super(ListSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
        self.element_type = element_type
        self.max_items = max_items
        self.min_items = min_items
        self.unique_items = unique_items

    @property
    def serialization_type(self) -> str:
        return f"[{self.element_type.serialization_type}]"

    @property
    def type_annotation(self) -> str:
        return f"List[{self.element_type.type_annotation}]"

    @property
    def operation_type_annotation(self) -> str:
        return f"List[{self.element_type.operation_type_annotation}]"

    @property
    def docstring_type(self) -> str:
        return f"list[{self.element_type.docstring_type}]"

    @property
    def docstring_text(self) -> str:
        return f"list of {self.element_type.docstring_text}"

    @property
    def validation_map(self) -> Optional[Dict[str, Union[bool, int, str]]]:
        validation_map: Dict[str, Union[bool, int, str]] = {}
        if self.max_items:
            validation_map["max_items"] = self.max_items
            validation_map["min_items"] = self.min_items or 0
        if self.min_items:
            validation_map["min_items"] = self.min_items
        if self.unique_items:
            validation_map["unique"] = True
        return validation_map or None

    @property
    def has_xml_serialization_ctxt(self) -> bool:
        return super().has_xml_serialization_ctxt or self.element_type.has_xml_serialization_ctxt

    def get_json_template_representation(self, **kwargs: Any) -> Any:
        return [self.element_type.get_json_template_representation(**kwargs)]

    def get_files_template_representation(self, **kwargs: Any) -> Any:
        return [self.element_type.get_files_template_representation(**kwargs)]

    def xml_serialization_ctxt(self) -> Optional[str]:
        attrs_list = []
        base_xml_map = super().xml_serialization_ctxt()
        if base_xml_map:
            attrs_list.append(base_xml_map)

        # Attribute at the list level
        if self.xml_metadata.get("wrapped", False):
            attrs_list.append("'wrapped': True")

        # Attributes of the items
        item_xml_metadata = self.element_type.xml_metadata
        if item_xml_metadata.get("name"):
            attrs_list.append(f"'itemsName': '{item_xml_metadata['name']}'")
        if item_xml_metadata.get("prefix", False):
            attrs_list.append(f"'itemsPrefix': '{item_xml_metadata['prefix']}'")
        if item_xml_metadata.get("namespace", False):
            attrs_list.append(f"'itemsNs': '{item_xml_metadata['namespace']}'")

        return ", ".join(attrs_list)

    @classmethod
    def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> "ListSchema":
        # TODO: for items, if the type is a primitive is it listed in type instead of $ref?
        element_schema = yaml_data["elementType"]

        from . import build_schema  # pylint: disable=import-outside-toplevel

        element_type = build_schema(yaml_data=element_schema, **kwargs)

        return cls(
            namespace=namespace,
            yaml_data=yaml_data,
            element_type=element_type,
            max_items=yaml_data.get("maxItems"),
            min_items=yaml_data.get("minItems"),
            unique_items=yaml_data.get("uniqueItems"),
        )

    def imports(self) -> FileImport:
        file_import = FileImport()
        file_import.add_from_import("typing", "List", ImportType.STDLIB, TypingSection.CONDITIONAL)
        file_import.merge(self.element_type.imports())
        return file_import
