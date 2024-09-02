# test_class_name_too_long
class ThisClassNameShouldEndUpBeingTooLongForAClient():  # @
    def __init__(self, **kwargs):
        pass


class ClassNameGoodClient():  # @
    # test_function_name_too_long
    def this_function_name_should_be_too_long_for_rule(self, **kwargs):  # @
        pass

    # test_variable_name_too_long
    def this_function_good(self, **kwargs):  # @
        this_lists_name_is_too_long_to_work_with_linter_rule = []  # @

    # test_private_name_too_long
    def _this_function_is_private_but_over_length_reqs(self, **kwargs):  # @
        this_lists_name = []  # @

    # test_instance_attr_name_too_long
    def __init__(self, this_name_is_too_long_to_use_anymore_reqs, **kwargs):  # @
        self.this_name_is_too_long_to_use_anymore_reqs = 10  # @

    # test_class_var_name_too_long
    this_name_is_too_long_to_use_anymore_reqs = 10  # @

    def __init__(self, dog, **kwargs):  # @
        self.dog = dog  # @
