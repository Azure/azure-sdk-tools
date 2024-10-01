# test_disallowed_typing
def function(x):  # @
    # type: (str) -> str
    pass


# test_allowed_typing
def function(x: str) -> str:  # @
    pass


# test_arbitrary_comments
def function(x):  # @
    # This is a comment
    pass

