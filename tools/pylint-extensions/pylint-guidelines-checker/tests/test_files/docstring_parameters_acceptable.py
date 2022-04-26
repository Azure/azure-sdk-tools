from typing import Union

def with_variadic_typehints(self, *var, **kwargs):
    """ Typehint
    :param str var: Args
    :param Any kwargs: Kwargs
    :rtype: None
    """
    # var is a vararg, which is classified differently than an arg
    pass

def with_ivar_typehints(self,something: str, something2: str, union: Union[bool, int]):
    """
    :ivar str something: Something
    :ivar str something2: Something2
    :ivar union: Union
    :vartype union: Union[bool, int]
    """
    pass