# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import List
from jinja2 import Environment
from .model_base_serializer import ModelBaseSerializer
from ..models import ObjectSchema, CodeModel, Property


class ModelGenericSerializer(ModelBaseSerializer):

    def __init__(self, code_model: CodeModel, env: Environment) -> None:
        super(ModelGenericSerializer, self).__init__(
            code_model=code_model, env=env, is_python_3_file=False
        )

    def init_line(self, model: ObjectSchema) -> List[str]:
        return []

    def properties_to_pass_to_super(self, model: ObjectSchema) -> str:
        return "**kwargs"

    def required_property_no_default_init(self, prop: Property) -> str:
        return f"self.{prop.name} = kwargs['{prop.name}']"

    def optional_property_init(self, prop: Property) -> str:
        default = prop.default_value_declaration
        return f"self.{prop.name} = kwargs.get('{prop.name}', {default})"

    def initialize_standard_arg(self, prop: Property) -> str:
        return self.initialize_standard_property(prop)
