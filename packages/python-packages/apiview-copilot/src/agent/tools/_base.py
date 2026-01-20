# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------


class Tool:
    """Base class for agent tools. Provides automatic tool discovery."""

    def all_tools(self):
        """Return all non-internal callable methods for agent registration."""
        tools = [
            getattr(self, name)
            for name in dir(self)
            if not name.startswith("_") and callable(getattr(self, name)) and name != "all_tools"
        ]
        return tools
