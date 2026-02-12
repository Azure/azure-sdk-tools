# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,unused-argument

"""Tests for ``db`` CLI commands (get, delete, purge)."""

from unittest.mock import MagicMock, patch


class TestDbGet:
    """Tests for `db get` command."""

    @patch("cli.DatabaseManager.get_instance")
    def test_db_get_retrieves_item(self, mock_get_instance, capsys):
        """Validate db get retrieves an item from the database."""
        from cli import db_get

        mock_db = MagicMock()
        mock_container = MagicMock()
        mock_container.get.return_value = {"id": "item-1", "content": "test data"}
        mock_db.get_container_client.return_value = mock_container
        mock_get_instance.return_value = mock_db

        db_get(container_name="guidelines", id="item-1")

        mock_db.get_container_client.assert_called_once_with("guidelines")
        mock_container.get.assert_called_once_with("item-1")
        captured = capsys.readouterr()
        assert "item-1" in captured.out

    @patch("cli.DatabaseManager.get_instance")
    def test_db_get_not_found(self, mock_get_instance, capsys):
        """Validate db get handles missing items."""
        from cli import db_get

        mock_db = MagicMock()
        mock_container = MagicMock()
        mock_container.get.side_effect = Exception("Not found")
        mock_db.get_container_client.return_value = mock_container
        mock_get_instance.return_value = mock_db

        db_get(container_name="guidelines", id="missing-item")

        captured = capsys.readouterr()
        assert "Error" in captured.out


class TestDbDelete:
    """Tests for `db delete` command."""

    @patch("cli.DatabaseManager.get_instance")
    def test_db_delete_soft_deletes(self, mock_get_instance, capsys):
        """Validate db delete performs a soft-delete."""
        from cli import db_delete

        mock_db = MagicMock()
        mock_container = MagicMock()
        mock_db.get_container_client.return_value = mock_container
        mock_get_instance.return_value = mock_db

        db_delete(container_name="guidelines", id="item-1")

        mock_container.delete_item.assert_called_once_with(item="item-1", partition_key="item-1")
        captured = capsys.readouterr()
        assert "soft-deleted" in captured.out.lower()


class TestDbPurge:
    """Tests for `db purge` command."""

    @patch("cli.GarbageCollector")
    def test_db_purge_all_containers(self, mock_gc_cls, capsys):
        """Validate db purge purges all data containers."""
        from cli import db_purge
        from src._database_manager import ContainerNames

        mock_gc = MagicMock()
        mock_gc.get_item_count.return_value = 0
        mock_gc_cls.return_value = mock_gc

        db_purge()

        expected_containers = ContainerNames.data_containers()
        assert mock_gc.purge_items.call_count == len(expected_containers)

    @patch("cli.GarbageCollector")
    def test_db_purge_specific_containers(self, mock_gc_cls, capsys):
        """Validate db purge with specific containers."""
        from cli import db_purge

        mock_gc = MagicMock()
        mock_gc.get_item_count.return_value = 5
        mock_gc_cls.return_value = mock_gc

        db_purge(containers=["guidelines"])

        mock_gc.purge_items.assert_called_once_with("guidelines")

    @patch("cli.GarbageCollector")
    def test_db_purge_with_indexer(self, mock_gc_cls):
        """Validate db purge with run_indexer=True uses different method."""
        from cli import db_purge

        mock_gc = MagicMock()
        mock_gc.get_item_count.return_value = 0
        mock_gc_cls.return_value = mock_gc

        db_purge(containers=["guidelines"], run_indexer=True)

        mock_gc.run_indexer_and_purge.assert_called_once_with("guidelines")
        mock_gc.purge_items.assert_not_called()
