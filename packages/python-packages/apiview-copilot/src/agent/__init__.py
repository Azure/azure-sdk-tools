# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Initialize the agent module for APIView Copilot.
"""

from ._agent import get_readwrite_agent

__all__ = ["get_readwrite_agent"]
