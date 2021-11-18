# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import DocstringParser


docstring_standard_return_type = """
Dummy docstring to verify standard return types and param types
:rtype: str
"""

docstring_Union_return_type1 = """
Dummy docstring to verify standard return types and param types
:rtype: Union[str, int]
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

class TestDocstringParser:

    def _test_variable_type(self, docstring, expected):
        parser = DocstringParser(docstring)
        for varname, expect_val in expected.items():
            assert expect_val == parser.type_for(varname)

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
            "country_hint": "str",
            "model_version": "str",
            "show_stats": "bool"
        })

    def test_docstring_param_type_private(self):
        self._test_variable_type(docstring_param_type_private, {
            "name": "str",
            "client": "~azure.search.documents._search_index_document_batching_client_base.SearchIndexDocumentBatchingClientBase"
        })

    def test_return_builtin_return_type(self):
        self._test_return_type(docstring_standard_return_type, "str")

    def test_return_union_return_type(self):
        self._test_return_type(docstring_Union_return_type1, "Union[str, int]")
    
    def test_return_union_return_type_followed_by_irrelevant_text(self):
        self._test_return_type(docstring_union_return_type_followed_by_irrelevant_text, "Union(str, int)")

    def test_return_union_lower_case_return_type(self):
        self._test_return_type(docstring_union_return_type3, "union[str, int]")

    def test_multi_return_type(self):
        self._test_return_type(docstring_multi_ret_type, "str or ~azure.test.testclass or None")

    def test_dict_return_type(self):
        self._test_return_type(docstring_dict_ret_type, "dict[str, int]")
