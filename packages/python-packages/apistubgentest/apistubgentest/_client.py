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

    def func_with_complex_python2_typehints(self):
        # type: (...) -> AnalyzeSometingLROPoller[ItemPaged[Union[AnalyzeSomethingResult, DocumentError]]]  # pylint: disable=line-too-long
        pass
