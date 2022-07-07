from typing import Union

def with_variadic_typehints(self, *var, **kwargs):
    """ Typehint
    :keyword str var: Args
    :keyword Any kwargs: Kwargs
    :rtype: None
    :return: None
    """
    # """ Typehint
    # :param str var: Args
    # :param Any kwargs: Kwargs
    # :rtype: None
    # :return: None
    # """
    # var is a vararg, which is classified differently than an arg
    #instance variable is ivar  -> would var be a param or would it be an args and would kwargs be a param or would it be keyword , and should either of these be in the docstring 
    pass

def with_ivar_typehints(self, something: str, something2: str, union: Union[bool, int]):
    """
    :ivar str something: Something
    :ivar str something2: Something2
    :ivar union: Union
    :vartype union: Union[bool, int]
    """
    pass
