# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Dict
from pathlib import Path
from jinja2 import Environment, PackageLoader

from ...jsonrpc import AutorestAPI


class MultiAPISerializer:
    def __init__(
        self, conf: Dict[str, Any], async_mode: bool, autorestapi: AutorestAPI, service_client_filename: str
    ):
        self.conf = conf
        self.async_mode = async_mode
        self._autorestapi = autorestapi
        self.service_client_filename = service_client_filename
        self.env = Environment(
            loader=PackageLoader("autorest.multiapi", "templates"),
            keep_trailing_newline=True,
            line_statement_prefix="##",
            line_comment_prefix="###",
            trim_blocks=True,
            lstrip_blocks=True,
        )

    def _get_file_path(self, filename: str) -> Path:
        if self.async_mode:
            return Path("aio") / filename
        return Path(filename)

    def serialize(self):
        self._autorestapi.write_file(self._get_file_path("__init__.py"), self.serialize_multiapi_init())

        service_client_filename_with_py_extension = self.service_client_filename + ".py"
        self._autorestapi.write_file(
            self._get_file_path(service_client_filename_with_py_extension),
            self.serialize_multiapi_client()
        )

        configuration_filename = "_configuration.py"
        self._autorestapi.write_file(
            self._get_file_path(configuration_filename),
            self.serialize_multiapi_config()
        )

        operation_mixins_filename = "_operations_mixin.py"
        if self.conf["mixin_operations"]:
            self._autorestapi.write_file(
                self._get_file_path(operation_mixins_filename),
                self.serialize_multiapi_operation_mixins()
            )

        if self._autorestapi.read_file("_version.py"):
            self._autorestapi.write_file(
                "_version.py",
                self._autorestapi.read_file("_version.py")
            )
        elif self._autorestapi.read_file("version.py"):
            self._autorestapi.write_file(
                "_version.py",
                self._autorestapi.read_file("version.py")
            )
        else:
            self._autorestapi.write_file(
                Path("_version.py"),
                self.serialize_multiapi_version()
            )

        # don't erase patch file
        if self._autorestapi.read_file("_patch.py"):
            self._autorestapi.write_file(
                "_patch.py",
                self._autorestapi.read_file("_patch.py")
            )

        self._autorestapi.write_file(
            Path("models.py"),
            self.serialize_multiapi_models()
        )

        self._autorestapi.write_file(Path("py.typed"), "# Marker file for PEP 561.")


    def serialize_multiapi_init(self) -> str:
        template = self.env.get_template("multiapi_init.py.jinja2")
        return template.render(
            service_client_filename=self.service_client_filename,
            client_name=self.conf["client_name"],
            async_mode=self.async_mode
        )

    def serialize_multiapi_client(self) -> str:
        template = self.env.get_template("multiapi_service_client.py.jinja2")
        return template.render(**self.conf, async_mode=self.async_mode)

    def serialize_multiapi_config(self) -> str:
        template = self.env.get_template("multiapi_config.py.jinja2")
        return template.render(**self.conf, async_mode=self.async_mode)

    def serialize_multiapi_models(self) -> str:
        template = self.env.get_template("multiapi_models.py.jinja2")
        return template.render(**self.conf)

    def serialize_multiapi_version(self) -> str:
        template = self.env.get_template("multiapi_version.py.jinja2")
        return template.render()

    def serialize_multiapi_operation_mixins(self) -> str:
        template = self.env.get_template("multiapi_operations_mixin.py.jinja2")
        return template.render(**self.conf, async_mode=self.async_mode)
