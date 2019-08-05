# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from .version import VERSION

__version__ = VERSION

from .monorepo import *

__all__ = ["monorepo", "__version__"]
