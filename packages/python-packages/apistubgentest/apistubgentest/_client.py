from typing import Optional, Union

class TypeHintingClient:

    def some_method_non_optional(docstring_type = "string", typehint_type: str = "string", **kwargs) -> None:
        """ Some method
        :param str docstring_type: Some string with a default value.
        """
        pass

    def some_method_with_optionals(labeled_optional: Optional[str] = "string", union_with_none: Union[str, None] = "string", **kwargs) -> None:
        pass

    def func_with_python2_typehints(
        self,
        name, # type: str
        age # type: int
    ):
        # type: (...) -> str
        return "{} is {} years old.".format(name, age)

    # def test_typehint(self):
    #     parser = TypeHintParser([])
    #     code = """
    #     # type: (str) -> str
    #     return val
    #     """
    #     expected = "str"
    #     assert parser._parse_typehint(code) == expected

    # def test_typehint_no_spaces(self):
    #     parser = TypeHintParser([])
    #     code = """
    #     # type:(str)->str
    #     return val
    #     """
    #     expected = "str"
    #     assert parser._parse_typehint(code) == expected

    # def test_typehint_with_pylint_disable(self):
    #     parser = TypeHintParser([])
    #     code = """
    #     # type: (...) -> AnalyzeHealthcareEntitiesLROPoller[ItemPaged[Union[AnalyzeHealthcareEntitiesResult, DocumentError]]]  # pylint: disable=line-too-long
    #     """
    #     expected = "AnalyzeHealthcareEntitiesLROPoller[ItemPaged[Union[AnalyzeHealthcareEntitiesResult, DocumentError]]]"
    #     assert parser._parse_typehint(code) == expected

