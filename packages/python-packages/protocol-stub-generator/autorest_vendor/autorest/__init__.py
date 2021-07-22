# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import logging
from abc import ABC, abstractmethod
from typing import Any, Dict

import yaml

from .jsonrpc import AutorestAPI
from ._version import VERSION


__version__ = VERSION
_LOGGER = logging.getLogger(__name__)


class Plugin(ABC):
    """A base class for autorest plugin.

    :param autorestapi: An autorest API instance
    """

    def __init__(self, autorestapi: AutorestAPI) -> None:
        self._autorestapi = autorestapi

    @abstractmethod
    def process(self) -> bool:
        """The plugin process.

        :rtype: bool
        :returns: True if everything's ok, False optherwise
        :raises Exception: Could raise any exception, stacktrace will be sent to autorest API
        """
        raise NotImplementedError()


class YamlUpdatePlugin(Plugin):
    """A plugin that update the YAML as input.
    """

    def process(self) -> bool:
        # List the input file, should be only one
        inputs = self._autorestapi.list_inputs()
        _LOGGER.debug("Possible Inputs: %s", inputs)
        if "code-model-v4-no-tags.yaml" not in inputs:
            raise ValueError("code-model-v4-no-tags.yaml must be a possible input")

        file_content = self._autorestapi.read_file("code-model-v4-no-tags.yaml")
        yaml_data = yaml.safe_load(file_content)

        self.update_yaml(yaml_data)

        yaml_string = yaml.safe_dump(yaml_data)

        self._autorestapi.write_file("code-model-v4-no-tags.yaml", yaml_string)
        return True

    @abstractmethod
    def update_yaml(self, yaml_data: Dict[str, Any]) -> None:
        """The code-model-v4-no-tags yaml model tree.

        :rtype: None
        :raises Exception: Could raise any exception, stacktrace will be sent to autorest API
        """
        raise NotImplementedError()


__all__ = ["Plugin", "YamlUpdatePlugin"]
