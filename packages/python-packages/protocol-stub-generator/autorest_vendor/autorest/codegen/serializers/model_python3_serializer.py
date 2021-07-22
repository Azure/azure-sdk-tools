# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import cast, List
from jinja2 import Environment
from .model_base_serializer import ModelBaseSerializer
from ..models import ObjectSchema, CodeModel, Property
from ..models.imports import FileImport


class ModelPython3Serializer(ModelBaseSerializer):

    def __init__(self, code_model: CodeModel, env: Environment) -> None:
        super(ModelPython3Serializer, self).__init__(
            code_model=code_model, env=env, is_python_3_file=True
        )

    def init_line(self, model: ObjectSchema) -> List[str]:
        init_properties_declaration = []
        init_line_parameters = [
            p for p in model.properties if not p.readonly and not p.is_discriminator and not p.constant
        ]
        init_line_parameters.sort(key=lambda x: x.required, reverse=True)
        if init_line_parameters:
            init_properties_declaration.append("*")
        for param in init_line_parameters:
            init_properties_declaration.append(self.initialize_standard_property(param))

        return init_properties_declaration

    def properties_to_pass_to_super(self, model: ObjectSchema) -> str:
        properties_to_pass_to_super = []
        for uncast_base_model in model.base_models:
            base_model = cast(ObjectSchema, uncast_base_model)
            for prop in model.properties:
                if (
                    prop in base_model.properties
                    and not prop.is_discriminator
                    and not prop.constant
                    and not prop.readonly
                ):
                    properties_to_pass_to_super.append(f"{prop.name}={prop.name}")
        properties_to_pass_to_super.append("**kwargs")
        return ", ".join(properties_to_pass_to_super)

    def required_property_no_default_init(self, prop: Property) -> str:
        return f"{prop.name}: {prop.type_annotation}"

    def optional_property_init(self, prop: Property) -> str:
        default = prop.default_value_declaration
        return f"{prop.name}: {prop.type_annotation} = {default}"

    def initialize_standard_arg(self, prop: Property) -> str:
        return f"self.{prop.name} = {prop.name}"

    def imports(self) -> FileImport:
        file_import = super(ModelPython3Serializer, self).imports()
        for model in self.code_model.sorted_schemas:
            init_line_parameters = [p for p in model.properties if not p.readonly and not p.is_discriminator]
            for param in init_line_parameters:
                file_import.merge(param.model_file_imports())

        return file_import
