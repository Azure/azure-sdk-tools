# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import io
import os
import tarfile
import zipfile

import pytest

from apistub._stub_generator import (
    _resolve_archive_member,
    _safe_extract_tar,
    _safe_extract_zip,
)

# Names that must never be written, whichever archive format carries them.
ESCAPING_NAMES = [
    "../escape.txt",
    "../../escape.txt",
    "pkg/../../escape.txt",
    "..",
    "/tmp/escape.txt",
    "//host/share/escape.txt",
    "C:/escape.txt",
    "c:/escape.txt",
    "C:\\escape.txt",
    "\\\\host\\share\\escape.txt",
    "..\\escape.txt",
]

# Tar member types that can point outside the extraction root even when their
# own name sits inside it, plus the special files a package never needs.
NON_REGULAR_TAR_TYPES = [
    (tarfile.SYMTYPE, "/etc/passwd"),
    (tarfile.LNKTYPE, "pkg/__init__.py"),
    (tarfile.FIFOTYPE, ""),
    (tarfile.CHRTYPE, ""),
    (tarfile.BLKTYPE, ""),
]


def _add_tar_file(tar, name, data=b"payload"):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    tar.addfile(info, io.BytesIO(data))


def _write_tar(path, names):
    with tarfile.open(path, "w:gz") as tar:
        for name in names:
            _add_tar_file(tar, name)


def _write_zip(path, names):
    with zipfile.ZipFile(path, "w") as zf:
        for name in names:
            # Assigning ``filename`` after construction keeps the name exactly
            # as written; ``ZipInfo`` rewrites ``os.sep`` to "/" otherwise, so a
            # backslash case would only be meaningful on Windows.
            info = zipfile.ZipInfo("placeholder")
            info.filename = name
            zf.writestr(info, b"payload")


def _tree(root):
    found = set()
    for dirpath, _, filenames in os.walk(root):
        for filename in filenames:
            found.add(os.path.relpath(os.path.join(dirpath, filename), root))
    return found


class TestArchiveExtraction:
    """Containment tests for sdist and wheel extraction.

    Each case asserts both halves of the fix: the archive is refused, and
    nothing is left behind either outside or inside the extraction directory.
    """

    def _dirs(self, tmp_path):
        dest = tmp_path / "dest"
        dest.mkdir()
        outside = tmp_path / "outside"
        outside.mkdir()
        return dest, outside

    @pytest.mark.parametrize("name", ESCAPING_NAMES)
    def test_tar_escaping_member_is_rejected(self, tmp_path, name):
        dest, outside = self._dirs(tmp_path)
        archive = tmp_path / "pkg.tar.gz"
        _write_tar(archive, [name])

        with pytest.raises(ValueError):
            _safe_extract_tar(str(archive), str(dest))

        assert _tree(dest) == set()
        assert _tree(outside) == set()

    @pytest.mark.parametrize("name", ESCAPING_NAMES)
    def test_zip_escaping_member_is_rejected(self, tmp_path, name):
        dest, outside = self._dirs(tmp_path)
        archive = tmp_path / "pkg.whl"
        _write_zip(archive, [name])

        with pytest.raises(ValueError):
            _safe_extract_zip(str(archive), str(dest))

        assert _tree(dest) == set()
        assert _tree(outside) == set()

    @pytest.mark.parametrize("member_type,linkname", NON_REGULAR_TAR_TYPES)
    def test_tar_non_regular_member_is_rejected(self, tmp_path, member_type, linkname):
        dest, outside = self._dirs(tmp_path)
        archive = tmp_path / "pkg.tar.gz"
        with tarfile.open(archive, "w:gz") as tar:
            _add_tar_file(tar, "pkg/__init__.py", b"")
            info = tarfile.TarInfo("pkg/secrets")
            info.type = member_type
            info.linkname = linkname
            tar.addfile(info)

        with pytest.raises(ValueError):
            _safe_extract_tar(str(archive), str(dest))

        assert _tree(dest) == set()
        assert _tree(outside) == set()

    def test_tar_aborts_before_writing_any_member(self, tmp_path):
        """A bad member anywhere in the archive must leave the directory empty.

        Validating during extraction would leave the earlier members on disk,
        and the parser would then install and import a partially unpacked
        package.
        """
        dest, _ = self._dirs(tmp_path)
        archive = tmp_path / "pkg.tar.gz"
        _write_tar(
            archive,
            [
                "pkg-1.0.0/pkg/__init__.py",
                "pkg-1.0.0/pkg/client.py",
                "../escape.txt",
            ],
        )

        with pytest.raises(ValueError):
            _safe_extract_tar(str(archive), str(dest))

        assert _tree(dest) == set()

    def test_zip_aborts_before_writing_any_member(self, tmp_path):
        dest, _ = self._dirs(tmp_path)
        archive = tmp_path / "pkg.whl"
        _write_zip(
            archive,
            ["pkg/__init__.py", "pkg/client.py", "../escape.txt"],
        )

        with pytest.raises(ValueError):
            _safe_extract_zip(str(archive), str(dest))

        assert _tree(dest) == set()

    def test_well_formed_sdist_still_extracts(self, tmp_path):
        dest, _ = self._dirs(tmp_path)
        archive = tmp_path / "pkg.tar.gz"
        _write_tar(
            archive,
            [
                "pkg-1.0.0/setup.py",
                "pkg-1.0.0/pkg/__init__.py",
                "pkg-1.0.0/pkg/nested/deep.py",
                "./pkg-1.0.0/pkg/dotted.py",
            ],
        )

        _safe_extract_tar(str(archive), str(dest))

        assert _tree(dest) == {
            os.path.join("pkg-1.0.0", "setup.py"),
            os.path.join("pkg-1.0.0", "pkg", "__init__.py"),
            os.path.join("pkg-1.0.0", "pkg", "nested", "deep.py"),
            os.path.join("pkg-1.0.0", "pkg", "dotted.py"),
        }

    def test_well_formed_wheel_still_extracts(self, tmp_path):
        dest, _ = self._dirs(tmp_path)
        archive = tmp_path / "pkg.whl"
        _write_zip(
            archive,
            [
                "pkg/__init__.py",
                "pkg/nested/deep.py",
                "pkg-1.0.0.dist-info/METADATA",
                "pkg-1.0.0.dist-info/RECORD",
            ],
        )

        _safe_extract_zip(str(archive), str(dest))

        assert _tree(dest) == {
            os.path.join("pkg", "__init__.py"),
            os.path.join("pkg", "nested", "deep.py"),
            os.path.join("pkg-1.0.0.dist-info", "METADATA"),
            os.path.join("pkg-1.0.0.dist-info", "RECORD"),
        }

    def test_directory_members_are_allowed(self, tmp_path):
        dest, _ = self._dirs(tmp_path)
        archive = tmp_path / "pkg.tar.gz"
        with tarfile.open(archive, "w:gz") as tar:
            for directory_name in (".", "pkg-1.0.0", "pkg-1.0.0/pkg"):
                directory = tarfile.TarInfo(directory_name)
                directory.type = tarfile.DIRTYPE
                tar.addfile(directory)
            _add_tar_file(tar, "pkg-1.0.0/pkg/__init__.py", b"")

        _safe_extract_tar(str(archive), str(dest))

        assert (dest / "pkg-1.0.0" / "pkg" / "__init__.py").is_file()

    def test_resolve_archive_member_returns_contained_path(self, tmp_path):
        dest, _ = self._dirs(tmp_path)
        resolved_dest = os.path.realpath(str(dest))

        resolved = _resolve_archive_member("pkg-1.0.0/pkg/__init__.py", resolved_dest)

        assert resolved == os.path.join(
            resolved_dest, "pkg-1.0.0", "pkg", "__init__.py"
        )

    def test_resolve_archive_member_rejects_null_byte(self, tmp_path):
        dest, _ = self._dirs(tmp_path)

        with pytest.raises(ValueError):
            _resolve_archive_member("pkg/\x00.py", os.path.realpath(str(dest)))

    def test_extraction_directory_reached_through_a_symlink(self, tmp_path):
        """Containment is decided on resolved paths, not on the caller's spelling.

        The parser is handed a temp directory it did not create, so that path
        may itself run through a symlink. Comparing unresolved paths would then
        reject every member of a perfectly good package.
        """
        real_dest = tmp_path / "real_dest"
        real_dest.mkdir()
        link = tmp_path / "linked_dest"
        try:
            link.symlink_to(real_dest, target_is_directory=True)
        except (OSError, NotImplementedError):
            pytest.skip("creating a directory symlink is not permitted here")

        archive = tmp_path / "pkg.tar.gz"
        _write_tar(archive, ["pkg-1.0.0/pkg/__init__.py"])

        _safe_extract_tar(str(archive), str(link))

        assert (real_dest / "pkg-1.0.0" / "pkg" / "__init__.py").is_file()
