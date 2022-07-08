from typing import Union

def with_variadic_typehints(self, *var, **kwargs):
    """ Typehint
    :param str var: Args
    :keyword something: something
    :paramtype something: str 
    :param Any kwargs: Kwargs
    :rtype: None
    :return: None
    """
    # param b/c this is a function
    # if it is a function = param 


    # var is a vararg, which is classified differently than an arg
    #instance variable is ivar  -> would var be a param or would it be an args and would kwargs be a param or would it be keyword , and should either of these be in the docstring 
    pass

class with_ivar_typehints():

    
    """
    :ivar str something: Something
    :param str something: Something 
    :ivar str something2: Something2
    :ivar union: Union
    :vartype union: bool or int 
    :rtype: None
    :return: None
    """
    def __init__(self, something: str, something2: str, union: Union[bool, int]):


        # self.something = an ivar
        # something = param 
        self.something = something
        self.something2 = something2
        # something3 is an ivar not a param 
        self.something3 = something + something2
