# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict, List, Optional, Union
from .base_schema import BaseSchema
from .dictionary_schema import DictionarySchema
from .property import Property
from .imports import FileImport, ImportType


class ObjectSchema(BaseSchema):  # pylint: disable=too-many-instance-attributes
    """Represents a class ready to be serialized in Python.

    :param str name: The name of the class.
    :param str description: The description of the class.
    :param properties: the optional properties of the class.
    :type properties: dict(str, str)
    """

    def __init__(
        self, namespace: str, yaml_data: Dict[str, Any], name: str, description: str = "", **kwargs
    ) -> None:
        super(ObjectSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
        self.name = name
        self.description = description
        self.max_properties: Optional[int] = kwargs.pop("max_properties", None)
        self.min_properties: Optional[int] = kwargs.pop("min_properties", None)
        self.properties: List[Property] = kwargs.pop("properties", [])
        self.is_exception: bool = kwargs.pop("is_exception", False)
        self.base_models: Union[List[int], List["ObjectSchema"]] = kwargs.pop("base_models", [])
        self.subtype_map: Optional[Dict[str, str]] = kwargs.pop("subtype_map", None)
        self.discriminator_name: Optional[str] = kwargs.pop("discriminator_name", None)
        self.discriminator_value: Optional[str] = kwargs.pop("discriminator_value", None)
        self._created_json_template_representation = False

    @property
    def serialization_type(self) -> str:
        return self.name

    @property
    def type_annotation(self) -> str:
        return f'"{self.name}"'

    @property
    def operation_type_annotation(self) -> str:
        return f'"_models.{self.name}"'

    @property
    def docstring_type(self) -> str:
        return f"~{self.namespace}.models.{self.name}"

    @property
    def docstring_text(self) -> str:
        return self.name

    def get_declaration(self, value: Any) -> str:
        return f"{self.name}()"

    def __repr__(self) -> str:
        return f"<{self.__class__.__name__} {self.name}>"

    @property
    def has_xml_serialization_ctxt(self) -> bool:
        return False

    def xml_serialization_ctxt(self) -> Optional[str]:
        # object schema contains _xml_map, they don't need serialization context
        return ""

    def xml_map_content(self) -> Optional[str]:
        if not self.xml_metadata:
            raise ValueError("This object does not contain XML metadata")
        # This is NOT an error on the super call, we use the serialization context for "xml_map",
        # but we don't want to write a serialization context for an object.
        return super().xml_serialization_ctxt()

    def get_json_template_representation(self, **kwargs: Any) -> Any:
        if self._created_json_template_representation:
            return "..."  # do this to avoid loop
        self._created_json_template_representation = True
        representation = {
            "{}".format(
                prop.original_swagger_name
            ): prop.get_json_template_representation(**kwargs)
            for prop in [p for p in self.properties if not p.is_discriminator]
        }
        try:
            # add discriminator prop if there is one
            discriminator = next(p for p in self.properties if p.is_discriminator)
            representation[
                discriminator.original_swagger_name
            ] = self.discriminator_value or discriminator.original_swagger_name
        except StopIteration:
            pass

        # once we've finished, we want to reset created_json_template_representation to false
        # so we can call it again
        self._created_json_template_representation = False
        return representation

    def get_files_template_representation(self, **kwargs: Any) -> Any:
        object_schema_names = kwargs.get("object_schema_names", [])
        object_schema_names.append(self.name)  # do tis to avoid circular
        kwargs["object_schema_names"] = object_schema_names
        return {
            "{}".format(
                prop.original_swagger_name
            ): prop.get_files_template_representation(**kwargs)
            for prop in self.properties
        }


    @classmethod
    def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> "ObjectSchema":
        """Returns a ClassType from the dict object constructed from a yaml file.

        WARNING: This guy might create an infinite loop.

        :param str name: The name of the class type.
        :param yaml_data: A representation of the schema of a class type from a yaml file.
        :type yaml_data: dict(str, str)
        :returns: A ClassType.
        :rtype: ~autorest.models.schema.ClassType
        """
        obj = cls(namespace, yaml_data, "", description="")
        obj.fill_instance_from_yaml(namespace, yaml_data)
        return obj

    def fill_instance_from_yaml(self, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> None:
        properties = []
        base_models = []

        name = yaml_data["language"]["python"]["name"]

        # checking to see if there is a parent class and / or additional properties
        if yaml_data.get("parents"):
            immediate_parents = yaml_data["parents"]["immediate"]
            # checking if object has a parent
            if immediate_parents:
                for immediate_parent in immediate_parents:
                    if immediate_parent["type"] == "dictionary":
                        additional_properties_schema = DictionarySchema.from_yaml(
                            namespace=namespace, yaml_data=immediate_parent, **kwargs
                        )
                        properties.append(
                            Property(
                                name="additional_properties",
                                schema=additional_properties_schema,
                                original_swagger_name="",
                                yaml_data={},
                                description="Unmatched properties from the message are deserialized to this collection."
                            )
                        )
                    elif (
                        immediate_parent["language"]["default"]["name"] != name and
                        immediate_parent['type'] == "object"
                    ):
                        base_models.append(id(immediate_parent))

        # checking to see if this is a polymorphic class
        subtype_map = None
        if yaml_data.get("discriminator"):
            subtype_map = {}
            # map of discriminator value to child's name
            for children_yaml in yaml_data["discriminator"]["immediate"].values():
                subtype_map[children_yaml["discriminatorValue"]] = children_yaml["language"]["python"]["name"]
        if yaml_data.get("properties"):
            properties += [
                Property.from_yaml(p, has_additional_properties=len(properties) > 0, **kwargs)
                for p in yaml_data["properties"]
            ]
        # this is to ensure that the attribute map type and property type are generated correctly



        description = yaml_data["language"]["python"]["description"]
        is_exception = False
        exceptions_set = kwargs.pop("exceptions_set", None)
        if exceptions_set:
            if id(yaml_data) in exceptions_set:
                is_exception = True

        self.yaml_data = yaml_data
        self.name = name
        self.description = description
        self.properties = properties
        self.base_models = base_models
        self.is_exception = is_exception
        self.subtype_map = subtype_map
        self.discriminator_name = (
            yaml_data["discriminator"]["property"]["language"]["python"]["name"]
            if yaml_data.get("discriminator")
            else None
        )
        self.discriminator_value = yaml_data.get("discriminatorValue", None)

    @property
    def has_readonly_or_constant_property(self) -> bool:
        return any(x.readonly or x.constant for x in self.properties)

    @property
    def property_with_discriminator(self) -> Any:
        try:
            return next(p for p in self.properties if getattr(p.schema, "discriminator_name", None))
        except StopIteration:
            return None

    def imports(self) -> FileImport:
        file_import = FileImport()
        if self.is_exception:
            file_import.add_from_import("azure.core.exceptions", "HttpResponseError", ImportType.AZURECORE)
        return file_import
