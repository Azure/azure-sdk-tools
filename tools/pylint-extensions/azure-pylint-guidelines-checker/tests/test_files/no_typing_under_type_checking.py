from typing import TYPE_CHECKING

# test_disallowed_import_from
if TYPE_CHECKING:
    from typing import Any  # @

# test_disallowed_import_from_extensions
if TYPE_CHECKING:
    import typing_extensions  # @

# test_allowed_imports
if TYPE_CHECKING:
    from math import PI

# test_allowed_import_else
if sys.version_info >= (3, 9):
    from collections.abc import MutableMapping
else:
    from typing import MutableMapping  # @
    import typing  # @
    import typing_extensions  # @
    from typing_extensions import Protocol  # @
