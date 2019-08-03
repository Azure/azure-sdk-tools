from .version import VERSION

__version__ = VERSION
version = VERSION


from .monorepo import *

__all__ = [ 
            'monorepo',
            '__version__',
            'version'
        ]

