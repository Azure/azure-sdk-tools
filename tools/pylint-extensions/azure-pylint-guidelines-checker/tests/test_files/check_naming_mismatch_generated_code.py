# test_import_naming_mismatch_violation
import Something
import Something2 as SomethingTwo

__all__ = (
    "Something",
    "SomethingTwo",
)

# test_import_from_naming_mismatch_violation
from Something2 import SomethingToo as SomethingTwo

# test_naming_mismatch_acceptable
__all__ = (
    "Something",
    "Something2",
)
