# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import contextlib
import os
import sys
import logging

from jsonrpc import dispatcher, JSONRPCResponseManager

from .stdstream import read_message, write_message


_LOGGER = logging.getLogger(__name__)


@dispatcher.add_method
def GetPluginNames():
    return ["codegen", "m2r", "namer", "black", "multiapiscript"]


@dispatcher.add_method
def Process(plugin_name: str, session_id: str) -> bool:
    # pylint: disable=import-outside-toplevel
    """JSON-RPC process call.
    """
    from .stdstream import StdStreamAutorestAPI

    with contextlib.closing(StdStreamAutorestAPI(session_id)) as stdstream_connection:

        _LOGGER.debug("Autorest called process with plugin_name '%s' and session_id: '%s'", plugin_name, session_id)
        if plugin_name == "m2r":
            from ..m2r import M2R as PluginToLoad
        elif plugin_name == "namer":
            from ..namer import Namer as PluginToLoad  # type: ignore
        elif plugin_name == "codegen":
            from ..codegen import CodeGenerator as PluginToLoad  # type: ignore
        elif plugin_name == "black":
            from ..black import BlackScriptPlugin  as PluginToLoad  # type: ignore
        elif plugin_name == "multiapiscript":
            from ..multiapi import MultiApiScriptPlugin as PluginToLoad  # type: ignore
        else:
            _LOGGER.fatal("Unknown plugin name %s", plugin_name)
            raise RuntimeError(f"Unknown plugin name {plugin_name}")

        plugin = PluginToLoad(stdstream_connection)

        try:
            _LOGGER.debug("Starting plugin %s", PluginToLoad.__name__)
            return plugin.process()
        except Exception:  # pylint: disable=broad-except
            _LOGGER.exception("Python generator raised an exception")
    return False


def main() -> None:
    # If --python.debugger is specified on the command line, we call the server.py file internally
    # with flag --debug.
    if '--debug' in sys.argv or os.environ.get("AUTOREST_PYTHON_ATTACH_VSCODE_DEBUG", False):
        try:
            import ptvsd  # pylint: disable=import-outside-toplevel
        except ImportError:
            raise SystemExit("Please pip install ptvsd in order to use VSCode debugging")

        # 5678 is the default attach port in the VS Code debug configurations
        ptvsd.enable_attach(address=("localhost", 5678), redirect_output=True)
        ptvsd.wait_for_attach()
        breakpoint()  # pylint: disable=undefined-variable

    _LOGGER.debug("Starting JSON RPC server")

    while True:
        _LOGGER.debug("Trying to read")
        message = read_message()

        response = JSONRPCResponseManager.handle(message, dispatcher).json
        _LOGGER.debug("Produced: %s", response)
        write_message(response)
        _LOGGER.debug("Message processed")

    _LOGGER.debug("Ending JSON RPC server")


if __name__ == "__main__":
    main()
