from .version import VERSION

__version__ = VERSION

from .monorepo import *

__all__ = ["monorepo", "__version__"]
