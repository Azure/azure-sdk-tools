# test_api_version_violation
class SomeClient(object):
    """
       :param str something: something
    """

    def __init__(self, something, **kwargs):
        pass


# test_api_version_acceptable
class Some1Client():  # @
    """
        :param str something: something
        :keyword str api_version: api_version
    """

    def __init__(self, something, **kwargs):
        pass
