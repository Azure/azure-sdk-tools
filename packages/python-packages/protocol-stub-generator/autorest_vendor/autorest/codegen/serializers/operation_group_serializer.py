# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import functools
from jinja2 import Environment

from .import_serializer import FileImportSerializer
from ..models import LROOperation, PagingOperation, CodeModel, OperationGroup
from .builder_serializer import get_operation_serializer


class OperationGroupSerializer:
    def __init__(
        self, code_model: CodeModel, env: Environment, operation_group: OperationGroup, async_mode: bool
    ) -> None:
        self.code_model = code_model
        self.env = env
        self.operation_group = operation_group
        self.async_mode = async_mode

    def serialize(self) -> str:
        def _is_lro(operation):
            return isinstance(operation, LROOperation)

        def _is_paging(operation):
            return isinstance(operation, PagingOperation)

        operation_group_template = self.env.get_template("operations_container.py.jinja2")
        if self.operation_group.is_empty_operation_group:
            operation_group_template = self.env.get_template("operations_container_mixin.py.jinja2")

        has_schemas = self.code_model.schemas or self.code_model.enums
        return operation_group_template.render(
            code_model=self.code_model,
            operation_group=self.operation_group,
            imports=FileImportSerializer(
                self.operation_group.imports(self.async_mode, bool(has_schemas)), is_python_3_file=self.async_mode
            ),
            async_mode=self.async_mode,
            is_lro=_is_lro,
            is_paging=_is_paging,
            get_operation_serializer=functools.partial(
                get_operation_serializer, code_model=self.code_model, async_mode=self.async_mode
            ),
        )
