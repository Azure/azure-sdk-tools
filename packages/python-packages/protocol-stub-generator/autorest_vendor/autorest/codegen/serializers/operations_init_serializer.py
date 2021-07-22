# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from jinja2 import Environment
from ..models import CodeModel


class OperationsInitSerializer:
    def __init__(self, code_model: CodeModel, env: Environment, async_mode: bool) -> None:
        self.code_model = code_model
        self.env = env
        self.async_mode = async_mode

    def serialize(self) -> str:
        operation_group_init_template = self.env.get_template("operations_container_init.py.jinja2")

        return operation_group_init_template.render(
            code_model=self.code_model, operation_groups=self.code_model.operation_groups, async_mode=self.async_mode
        )
