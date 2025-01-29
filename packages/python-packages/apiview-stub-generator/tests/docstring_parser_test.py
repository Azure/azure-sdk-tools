# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from pydoc import Doc
from apistub.nodes import DocstringParser, ClassNode
from apistubgentest.models import DocstringClass 

from ._test_util import _tokenize, _render_lines, _check_all


docstring_default_legacy = """
:param value: Some dummy value. Default value
  is "cat". Extra text.
:type value: str
:param another: Something else. Default value
  is dog. Extra text.
:type value: str
:param some_class: Some kind of class type. Default value is :py:class:`apistubgen.test.models.FakeObject`.
:type some_class: class
"""

docstring_union_return_type_followed_by_irrelevant_text = """
Dummy docstring to verify standard return types and param types
:rtype: Union(str, int)
Dummy string at new line
"""

docstring_union_return_type3 = """
Dummy docstring to verify standard return types and param types
:rtype: union[str, int]
"""

docstring_multi_ret_type = """
Dummy docstring to verify standard return types and param types
:rtype: str or ~azure.test.testclass or None
"""

docstring_dict_ret_type = """
Dummy docstring to verify standard return types and param types
:rtype: dict[str, int]
"""

docstring_param_type = """
:param str name: Dummy name param
:param val: Value type
:type val: str
"""

docstring_param_type_split = """
:param str name: Dummy name param
:param val: Value type
:type val: str
:param ~azure.core.pipeline pipeline: dummy pipeline param
:param pipe_id: pipeline id
:type pipe_id: Union[str, int]
:param data: Dummy data
:type data: str or 
~azure.dummy.datastream
"""

docstring_param_typing_optional = """
:param group: Optional group to check if user exists in.
:type group: typing.Optional[str]
"""

docstring_param_nested_union = """
:param dummyarg: Optional group to check if user exists in.
:type dummyarg: typing.Union[~azure.eventhub.EventDataBatch, List[~azure.eventhub.EventData]]
"""

docstring_param_multi_line_type_followed_by_irrelevant_text = """
:param dummyarg: Optional group to check if user exists in.
:type dummyarg: typing.Union[~azure.eventhub.EventDataBatch,
    List[~azure.eventhub.EventData]]
.. admonition:: Example:
.. literalinclude:: ../samples/sample_detect_language.py
    :start-after: [START batch_detect_language]
    :end-before: [END batch_detect_language]
    :language: python
    :dedent: 8
    :caption: Detecting language in a batch of documents.
"""


docstring_multi_complex_type = """
        :param documents: The set of documents to process as part of this batch.
            If you wish to specify the ID and country_hint on a per-item basis you must
            use as input a list[:class:`~azure.ai.textanalytics.DetectLanguageInput`] or a list of
            dict representations of :class:`~azure.ai.textanalytics.DetectLanguageInput`, like
            `{"id": "1", "country_hint": "us", "text": "hello world"}`.
        :type documents:
            list[str] or list[~azure.ai.textanalytics.DetectLanguageInput] or list[dict[str, str]]
        :keyword str country_hint: A country hint for the entire batch. Accepts two
            letter country codes specified by ISO 3166-1 alpha-2. Per-document
            country hints will take precedence over whole batch hints. Defaults to
            "US". If you don't want to use a country hint, pass the string "none".
        :keyword str model_version: This value indicates which model will
            be used for scoring, e.g. "latest", "2019-10-01". If a model-version
            is not specified, the API will default to the latest, non-preview version.
        :keyword bool show_stats: If set to true, response will contain document
            level statistics.
        :return: The combined list of :class:`~azure.ai.textanalytics.DetectLanguageResult` and
            :class:`~azure.ai.textanalytics.DocumentError` in the order the original documents were
            passed in.
        :rtype: list[~azure.ai.textanalytics.DetectLanguageResult,
            ~azure.ai.textanalytics.DocumentError]
        :raises ~azure.core.exceptions.HttpResponseError or TypeError or ValueError:
        .. admonition:: Example:
            .. literalinclude:: ../samples/sample_detect_language.py
                :start-after: [START batch_detect_language]
                :end-before: [END batch_detect_language]
                :language: python
                :dedent: 8
                :caption: Detecting language in a batch of documents.
"""

docstring_param_type_private = """
:param str name: Dummy name param
:param client: Value type
:type client: ~azure.search.documents._search_index_document_batching_client_base.SearchIndexDocumentBatchingClientBase
"""

docstring_type_with_quotes = """
:param name: Dummy name param
:type name: `str`
:keyword name2: Dummy name param
:paramtype name2: "str"
:ivar name3: Dummy name param
:vartype name3: 'str'
:rtype: list[`str`]
"""

class TestDocstringParser:

    pkg_namespace = "apistubgentest.models"

    def _test_variable_type(self, docstring, expected):
        parser = DocstringParser(docstring)
        for varname, expect_val in expected.items():
            actual = parser.type_for(varname)
            assert expect_val == actual

    def _test_return_type(self, docstring, expected):
        parser = DocstringParser(docstring)
        assert expected == parser.ret_type

    def test_docstring_param_type(self):
        self._test_variable_type(docstring_param_type, {
            "name": "str",
            "val": "str"
        })

    def test_docstring_param_type_split(self):
        self._test_variable_type(docstring_param_type_split, {
            "name": "str",
            "val": "str",
            "pipeline": "~azure.core.pipeline",
            "pipe_id": "Union[str, int]",
            "data": "str or ~azure.dummy.datastream"
        })

    def test_docstring_param_typing_optional(self):
        self._test_variable_type(docstring_param_typing_optional, {
            "group": "typing.Optional[str]"
        })

    def test_docstring_param_nested_union(self):
        self._test_variable_type(docstring_param_nested_union, {
            "dummyarg": "typing.Union[~azure.eventhub.EventDataBatch, List[~azure.eventhub.EventData]]"
        })

    def test_docstring_param_multi_line_type_followed_by_irrelevant_text(self):
        self._test_variable_type(docstring_param_multi_line_type_followed_by_irrelevant_text, {
            "dummyarg": "typing.Union[~azure.eventhub.EventDataBatch, List[~azure.eventhub.EventData]]"
        })

    def test_docstring_multi_complex_type(self):
        self._test_variable_type(docstring_multi_complex_type, {
            "documents": "list[str] or list[~azure.ai.textanalytics.DetectLanguageInput] or list[dict[str, str]]",
            "country_hint": "Optional[str]",
            "model_version": "Optional[str]",
            "show_stats": "Optional[bool]"
        })

    def test_docstring_param_type_private(self):
        self._test_variable_type(docstring_param_type_private, {
            "name": "str",
            "client": "~azure.search.documents._search_index_document_batching_client_base.SearchIndexDocumentBatchingClientBase"
        })
    
    def test_return_union_return_type_followed_by_irrelevant_text(self):
        self._test_return_type(docstring_union_return_type_followed_by_irrelevant_text, "Union(str, int)")

    def test_return_union_lower_case_return_type(self):
        self._test_return_type(docstring_union_return_type3, "union[str, int]")

    def test_multi_return_type(self):
        self._test_return_type(docstring_multi_ret_type, "str or ~azure.test.testclass or None")

    def test_dict_return_type(self):
        self._test_return_type(docstring_dict_ret_type, "dict[str, int]")

    def test_type_removes_quotes(self):
        self._test_variable_type(docstring_type_with_quotes, {
            "name": "str",
            "name2": "str",
            "name3": "str"
        })
        self._test_return_type(docstring_type_with_quotes, "list[str]")
    
    def test_defaults(self):
        parser = DocstringParser(docstring_multi_complex_type)
        # optional keyword-arguments are documented with "..."
        assert parser.default_for("country_hint") == "..."
        assert parser.default_for("documents") == None

    def test_docstring_defaults_legacy(self):
        parser = DocstringParser(docstring_default_legacy)
        assert parser.default_for("value") == "cat"
        assert parser.default_for("another") == "dog"
        assert parser.default_for("some_class") == ":py:class:`apistubgen.test.models.FakeObject`"

    def test_docstring_complex_argtype_ivar(self):
        obj = DocstringClass
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class DocstringClass:",
            "ivar name: str",
            "ivar values: Optional[dict[str, str]]", # TODO: Should Dict vs. dict be mixed in like this?
        ]
        _check_all(actuals, expected, obj)

        expected_init = [
            "def __init__(",
            "self, ",
            "name: Optional[str], ",
            "*args: Any, ",
            "*, ", # TODO: Second asterisk should be removed
            "values: Optional[Dict[str, str]] = ...",
            ") -> None"
        ]
        _check_all(actuals[4:11], expected_init, obj)
        # check that type_for returns the correct type for the given ivar
        complex_ivar_docstring = obj.__doc__
        parser = DocstringParser(complex_ivar_docstring)
        assert parser.type_for("name", keyword="param") == "str or None"
        # docstring parser wraps kwargs in Optional by default, so None should be stripped
        assert parser.type_for("values", keyword="keyword") == "Optional[dict[str, str]]"
        assert parser.type_for("name", keyword="ivar") == "str"
        assert parser.type_for("values", keyword="ivar") == "dict[str, str] or None"
