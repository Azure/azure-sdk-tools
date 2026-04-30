# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

# Acceptable: typing_extensions is in setup.py install_requires
import typing_extensions

# Acceptable: requests is in setup.py install_requires
import requests

# Acceptable: stdlib imports are fine (not in _KNOWN_THIRD_PARTY)
import os
import json
