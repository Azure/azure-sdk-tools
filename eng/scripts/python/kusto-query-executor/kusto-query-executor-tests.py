"""Tests to ensure kusto-query-executor blocks destructive queries."""

import pytest
import sys
import os
import types
from unittest.mock import MagicMock

# Stub azure SDK modules so the script can be imported without them installed
for mod_name in [
    "azure", "azure.identity", "azure.kusto", "azure.kusto.data",
    "azure.kusto.data.exceptions",
]:
    sys.modules[mod_name] = MagicMock()

sys.path.insert(0, os.path.dirname(__file__))
from importlib import import_module

mod = import_module("kusto-query-executor")
is_query_safe = mod.is_query_safe


class TestDestructiveQueriesBlocked:
    """Verify that all destructive KQL operations are rejected."""

    @pytest.mark.parametrize("query", [
        "DROP TABLE MyTable",
        "drop table MyTable",
        ".drop table MyTable",
        "CREATE TABLE MyTable (Col1: string)",
        "ALTER TABLE MyTable (Col1: string)",
        "RENAME TABLE MyTable TO NewTable",
        "DELETE FROM MyTable WHERE true",
        "INSERT INTO MyTable VALUES ('a')",
        "UPDATE MyTable SET Col1 = 'b'",
        "SET NOTRUNCATION; MyTable",
        "GRANT admin TO user@example.com",
        "DENY admin TO user@example.com",
        "REVOKE admin FROM user@example.com",
        "EXECUTE database script <| print 1",
        "EXECUTE_AND_CACHE database script <| print 1",
        ".ingest into table MyTable",
        ".append MyTable <| print 1",
        ".set MyTable <| print 1",
        ".set-or-append MyTable <| print 1",
        ".set-or-replace MyTable <| print 1",
    ])
    def test_destructive_query_rejected(self, query):
        with pytest.raises(ValueError, match="not allowed|read-only"):
            is_query_safe(query)


class TestSafeQueriesAllowed:
    """Verify that legitimate read-only queries are accepted."""

    @pytest.mark.parametrize("query", [
        "MyTable | take 10",
        "MyTable | where Timestamp > ago(1h)",
        "let x = 1; print x",
        "datatable(a:int)[1,2,3]",
        "print 'hello'",
        "StormEvents | summarize count() by State",
        "[ExternalTable] | take 5",
    ])
    def test_safe_query_accepted(self, query):
        assert is_query_safe(query) is True
