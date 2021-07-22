# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import List
from pathlib import Path
from jinja2 import PackageLoader, Environment

from ...jsonrpc import AutorestAPI
from ..models import CodeModel
from ..models.request_builder import RequestBuilder
from .enum_serializer import EnumSerializer
from .general_serializer import GeneralSerializer
from .model_generic_serializer import ModelGenericSerializer
from .model_init_serializer import ModelInitSerializer
from .model_python3_serializer import ModelPython3Serializer
from .operations_init_serializer import OperationsInitSerializer
from .operation_group_serializer import OperationGroupSerializer
from .metadata_serializer import MetadataSerializer
from .rest_serializer import RestPython3Serializer, RestGenericSerializer, RestSerializer

__all__ = [
    "JinjaSerializer",
]


class JinjaSerializer:
    def __init__(self, autorestapi: AutorestAPI) -> None:
        self._autorestapi = autorestapi

    def serialize(self, code_model: CodeModel) -> None:
        env = Environment(
            loader=PackageLoader("autorest.codegen", "templates"),
            keep_trailing_newline=True,
            line_statement_prefix="##",
            line_comment_prefix="###",
            trim_blocks=True,
            lstrip_blocks=True,
        )

        namespace_path = (
            Path(".") if code_model.options["no_namespace_folders"] else Path(*(code_model.namespace.split(".")))
        )

        # if there was a patch file before, we keep it
        if self._autorestapi.read_file(namespace_path / "_patch.py"):
            self._autorestapi.write_file(
                namespace_path / Path("_patch.py"),
                self._autorestapi.read_file(namespace_path / "_patch.py")
            )

        self._serialize_and_write_top_level_folder(code_model=code_model, env=env, namespace_path=namespace_path)
        if code_model.rest.request_builders:
            self._serialize_and_write_rest_layer(code_model=code_model, env=env, namespace_path=namespace_path)
            if not code_model.options["no_async"]:
                self._serialize_and_write_aio_top_level_folder(
                    code_model=code_model, env=env, namespace_path=namespace_path,
                )

        if code_model.show_operations and code_model.operation_groups:
            self._serialize_and_write_operations_folder(code_model=code_model, env=env, namespace_path=namespace_path)

            if code_model.options["multiapi"]:
                self._serialize_and_write_metadata(
                    code_model, env=env, namespace_path=namespace_path
                )

        if code_model.show_models and (code_model.schemas or code_model.enums):
            self._serialize_and_write_models_folder(code_model=code_model, env=env, namespace_path=namespace_path)


    def _serialize_and_write_models_folder(self, code_model: CodeModel, env: Environment, namespace_path: Path) -> None:
        # Write the models folder
        models_path = namespace_path / Path("models")
        if code_model.schemas:
            self._autorestapi.write_file(
                models_path / Path("_models.py"), ModelGenericSerializer(code_model=code_model, env=env).serialize()
            )
            self._autorestapi.write_file(
                models_path / Path("_models_py3.py"), ModelPython3Serializer(code_model=code_model, env=env).serialize()
            )
        if code_model.enums:
            self._autorestapi.write_file(
                models_path / Path(f"_{code_model.module_name}_enums.py"),
                EnumSerializer(code_model=code_model, env=env).serialize(),
            )
        self._autorestapi.write_file(
            models_path / Path("__init__.py"), ModelInitSerializer(code_model=code_model, env=env).serialize()
        )

    def _serialize_and_write_rest_layer(
        self, code_model: CodeModel, env: Environment, namespace_path: Path
    ) -> None:
        rest_path = namespace_path / Path(code_model.rest_layer_name)
        operation_group_names = {
            rb.operation_group_name for rb in code_model.rest.request_builders
        }

        for operation_group_name in operation_group_names:
            output_path = rest_path / Path(operation_group_name) if operation_group_name else rest_path
            request_builders = [
                r for r in code_model.rest.request_builders if r.operation_group_name == operation_group_name
            ]
            self._serialize_and_write_single_rest_layer(
                code_model, env, output_path, request_builders
            )
        if not "" in operation_group_names:
            self._autorestapi.write_file(
                rest_path / Path("__init__.py"), code_model.options['license_header']
            )


    def _serialize_and_write_single_rest_layer(
        self, code_model: CodeModel, env: Environment, output_path: Path, request_builders: List[RequestBuilder]
    ) -> None:

        # write generic request builders file
        self._autorestapi.write_file(
            output_path / Path("_request_builders.py"),
            RestGenericSerializer(
                code_model=code_model, env=env, request_builders=request_builders
            ).serialize_request_builders()
        )

        # write python3 request builders file
        self._autorestapi.write_file(
            output_path / Path("_request_builders_py3.py"),
            RestPython3Serializer(
                code_model=code_model, env=env, request_builders=request_builders
            ).serialize_request_builders()
        )

        # write rest init file
        self._autorestapi.write_file(
            output_path / Path("__init__.py"), RestSerializer(
                code_model=code_model, env=env, request_builders=request_builders
            ).serialize_init()
        )

    def _serialize_and_write_operations_folder(
        self, code_model: CodeModel, env: Environment, namespace_path: Path
    ) -> None:
        # write sync operations init file
        operations_init_serializer = OperationsInitSerializer(code_model=code_model, env=env, async_mode=False)
        self._autorestapi.write_file(
            namespace_path / Path(f"operations") / Path("__init__.py"), operations_init_serializer.serialize()
        )

        # write async operations init file
        if not code_model.options["no_async"]:
            operations_async_init_serializer = OperationsInitSerializer(code_model=code_model, env=env, async_mode=True)
            self._autorestapi.write_file(
                namespace_path / Path("aio") / Path(f"operations") / Path("__init__.py"),
                operations_async_init_serializer.serialize(),
            )

        for operation_group in code_model.operation_groups:
            # write sync operation group and operation files
            operation_group_serializer = OperationGroupSerializer(
                code_model=code_model, env=env, operation_group=operation_group, async_mode=False
            )
            self._autorestapi.write_file(
                namespace_path / Path(f"operations") / Path(f"{operation_group.filename}.py"),
                operation_group_serializer.serialize(),
            )

            if not code_model.options["no_async"]:
                # write async operation group and operation files
                operation_group_async_serializer = OperationGroupSerializer(
                    code_model=code_model, env=env, operation_group=operation_group, async_mode=True
                )
                self._autorestapi.write_file(
                    (
                        namespace_path
                        / Path("aio")
                        / Path(f"operations")
                        / Path(f"{operation_group.filename}.py")
                    ),
                    operation_group_async_serializer.serialize(),
                )


    def _serialize_and_write_version_file(
        self, code_model: CodeModel, namespace_path: Path, general_serializer: GeneralSerializer
    ):
        def _read_version_file(original_version_file_name: str) -> str:
            return self._autorestapi.read_file(namespace_path / original_version_file_name)

        def _write_version_file(original_version_file_name: str) -> None:
            self._autorestapi.write_file(
                namespace_path / Path("_version.py"),
                _read_version_file(original_version_file_name)
            )
        keep_version_file = code_model.options['keep_version_file']
        if keep_version_file and _read_version_file("_version.py"):
            _write_version_file(original_version_file_name="_version.py")
        elif keep_version_file and _read_version_file("version.py"):
            _write_version_file(original_version_file_name="version.py")
        elif code_model.options['package_version']:
            self._autorestapi.write_file(
                namespace_path / Path("_version.py"),
                general_serializer.serialize_version_file()
            )

    def _serialize_and_write_top_level_folder(
        self, code_model: CodeModel, env: Environment, namespace_path: Path
    ) -> None:
        general_serializer = GeneralSerializer(code_model=code_model, env=env, async_mode=False)

        if code_model.rest.request_builders:
            self._autorestapi.write_file(
                namespace_path / Path("__init__.py"), general_serializer.serialize_init_file()
            )
        else:
            self._autorestapi.write_file(
                namespace_path / Path("__init__.py"), general_serializer.serialize_pkgutil_init_file()
            )
        p = namespace_path.parent
        while p != Path("."):
            # write pkgutil init file
            self._autorestapi.write_file(
                p / Path("__init__.py"), general_serializer.serialize_pkgutil_init_file()
            )
            p = p.parent

        # Write the service client
        if code_model.rest.request_builders:
            self._autorestapi.write_file(
                namespace_path / Path(f"_{code_model.module_name}.py"),
                general_serializer.serialize_service_client_file()
            )

        self._serialize_and_write_version_file(code_model, namespace_path, general_serializer)

        # write the empty py.typed file
        self._autorestapi.write_file(namespace_path / Path("py.typed"), "# Marker file for PEP 561.")

        # Write the config file
        if code_model.rest.request_builders:
            self._autorestapi.write_file(
                namespace_path / Path("_configuration.py"), general_serializer.serialize_config_file()
            )

        # Write the setup file
        if code_model.options["basic_setup_py"]:
            self._autorestapi.write_file(Path("setup.py"), general_serializer.serialize_setup_file())

    def _serialize_and_write_aio_top_level_folder(
        self, code_model: CodeModel, env: Environment, namespace_path: Path
    ) -> None:
        aio_general_serializer = GeneralSerializer(code_model=code_model, env=env, async_mode=True)

        aio_path = namespace_path / Path("aio")

        # Write the __init__ file
        self._autorestapi.write_file(aio_path / Path("__init__.py"), aio_general_serializer.serialize_init_file())

        # Write the service client
        self._autorestapi.write_file(
            aio_path / Path(f"_{code_model.module_name}.py"),
            aio_general_serializer.serialize_service_client_file(),
        )

        # Write the config file
        self._autorestapi.write_file(
            aio_path / Path("_configuration.py"), aio_general_serializer.serialize_config_file()
        )


    def _serialize_and_write_metadata(self, code_model: CodeModel, env: Environment, namespace_path: Path) -> None:
        metadata_serializer = MetadataSerializer(code_model, env)
        self._autorestapi.write_file(namespace_path / Path("_metadata.json"), metadata_serializer.serialize())
