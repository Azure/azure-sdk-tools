# Test file for catching kwargs in docstring

# test_docstring_kwargs_incorrect - should trigger error
def function_with_kwargs_error(**kwargs):
    """
    Function with kwargs in docstring (incorrect).
    
    :keyword kwargs: Some description
    :return: None  
    :rtype: None
    """
    return None

# test_docstring_kwargs_correct_expanded - should not trigger error
def function_with_kwargs_expanded(*, arg1=None, arg2=None, **kwargs):
    """
    Function with expanded kwargs (correct).
    
    :keyword str arg1: Description of arg1
    :keyword int arg2: Description of arg2
    :return: None
    :rtype: None
    """
    return None

# test_docstring_other_keyword - should not trigger error
def function_with_other_keyword(*, other_arg=None):
    """
    Function with other keyword argument.
    
    :keyword str other_arg: Some other argument
    :return: None
    :rtype: None
    """
    return None

# test_docstring_kwargs_correct_format - should not trigger error  
def function_with_correct_kwargs_format(**kwargs):
    """
    Function with correct kwargs documentation format.
    
    :keyword Dict[str, Any] **kwargs: Additional keyword arguments
    :return: None
    :rtype: None
    """
    return None