# Test for CheckNamingMismatchGeneratedCode

# Dummy imports for testing purposes
class Something:
    pass

class somethingTwo:
    pass

__all__ = (
    Something,
    somethingTwo,  # pylint: disable=naming-mismatch
)
