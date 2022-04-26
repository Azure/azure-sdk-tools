from typing import Union

def with_variadic_typehints(self, *var, **kwargs):
    """ Typehint
    :param str var: Args
    :param Any kwargs: Kwargs
    :rtype: None
    """
    pass

def with_ivar_typehints(self,something: str, something2: str, union: Union[bool, int]):
    """
    :ivar str something: Something
    :ivar str something2: Something2
    :ivar union: Union
    :vartype union: Union[bool, int]
    """
    pass