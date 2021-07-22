# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from abc import abstractmethod
from typing import cast, List
from jinja2 import Environment
from ..models import EnumSchema, ObjectSchema, CodeModel, Property, ConstantSchema
from ..models.imports import FileImport, ImportType
from .import_serializer import FileImportSerializer


class ModelBaseSerializer:
    def __init__(self, code_model: CodeModel, env: Environment, is_python_3_file: bool) -> None:
        self.code_model = code_model
        self.env = env
        self.is_python_3_file = is_python_3_file

    def serialize(self) -> str:
        # Generate the models
        template = self.env.get_template("model_container.py.jinja2")
        return template.render(
            code_model=self.code_model,
            imports=FileImportSerializer(self.imports(), is_python_3_file=self.is_python_3_file),
            str=str,
            init_line=self.init_line,
            init_args=self.init_args,
            prop_documentation_string=ModelBaseSerializer.prop_documentation_string,
            prop_type_documentation_string=ModelBaseSerializer.prop_type_documentation_string,
        )

    def imports(self) -> FileImport:
        file_import = FileImport()
        file_import.add_import("msrest.serialization", ImportType.AZURECORE)
        for model in self.code_model.sorted_schemas:
            file_import.merge(model.imports())
        return file_import

    @staticmethod
    def get_properties_to_initialize(model: ObjectSchema) -> List[Property]:
        if model.base_models:
            properties_to_initialize = []
            for uncast_base_model in model.base_models:
                base_model = cast(ObjectSchema, uncast_base_model)
                for prop in model.properties:
                    if prop not in base_model.properties or prop.is_discriminator or prop.constant:
                        properties_to_initialize.append(prop)
        else:
            properties_to_initialize = model.properties
        return properties_to_initialize

    @staticmethod
    def prop_documentation_string(prop: Property) -> str:
        # building the param line of the property doc
        if prop.constant or prop.readonly:
            param_doc_string = f":ivar {prop.name}:"
        else:
            param_doc_string = f":param {prop.name}:"
        description = prop.description
        if description and description[-1] != ".":
            description += "."
        if prop.name == "tags":
            description = "A set of tags. " + description

        if prop.constant:
            description += f' Has constant value: {prop.constant_declaration}.'
        elif prop.required:
            if description:
                description = "Required. " + description
            else:
                description = "Required. "
        elif isinstance(prop.schema, ConstantSchema):
            description += (
                f" The only acceptable values to pass in are None and {prop.constant_declaration}. " +
                f"The default value is {prop.default_value_declaration}."
            )
        if prop.is_discriminator:
            description += "Constant filled by server. "
        if isinstance(prop.schema, EnumSchema):
            values = [prop.schema.enum_type.get_declaration(v.value) for v in prop.schema.values]
            description += " Possible values include: {}.".format(", ".join(values))
            if prop.schema.default_value:
                description += f' Default value: "{prop.schema.default_value}".'
        if description:
            param_doc_string += " " + description
        return param_doc_string

    @staticmethod
    def prop_type_documentation_string(prop: Property) -> str:
        # building the type line of the property doc
        if prop.constant or prop.readonly:
            type_doc_string = f":vartype {prop.name}: "
        else:
            type_doc_string = f":type {prop.name}: "
        type_doc_string += prop.schema.docstring_type
        return type_doc_string

    def init_args(self, model: ObjectSchema) -> List[str]:
        init_args = []
        properties_to_pass_to_super = self.properties_to_pass_to_super(model)
        init_args.append(f"super({model.name}, self).__init__({properties_to_pass_to_super})")
        for prop in ModelBaseSerializer.get_properties_to_initialize(model):
            if prop.is_discriminator:
                discriminator_value = f"'{model.discriminator_value}'" if model.discriminator_value else None
                if not discriminator_value:
                    typing = "Optional[str]"
                else:
                    typing = "str"
                init_args.append(f"self.{prop.name} = {discriminator_value}  # type: {typing}")
            elif prop.readonly:
                init_args.append(f"self.{prop.name} = None")
            elif not prop.constant:
                init_args.append(self.initialize_standard_arg(prop))
        return init_args

    def initialize_standard_property(self, prop: Property):
        if prop.required and not prop.default_value:
            return self.required_property_no_default_init(prop)
        return self.optional_property_init(prop)

    @abstractmethod
    def init_line(self, model: ObjectSchema) -> List[str]:
        ...

    @abstractmethod
    def properties_to_pass_to_super(self, model: ObjectSchema) -> str:
        ...

    @abstractmethod
    def required_property_no_default_init(self, prop: Property) -> str:
        ...

    @abstractmethod
    def optional_property_init(self, prop: Property) -> str:
        ...

    @abstractmethod
    def initialize_standard_arg(self, prop: Property) -> str:
        ...
