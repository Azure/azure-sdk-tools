# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from copy import deepcopy
from typing import Dict, Set, Optional, List
from ..models import ImportType, FileImport, TypingSection

def _serialize_package(package_name: str, module_list: Set[Optional[str]], delimiter: str) -> str:
    buffer = []
    if None in module_list:
        buffer.append(f"import {package_name}")
    if module_list != {None}:
        buffer.append(
            "from {} import {}".format(
                package_name, ", ".join(sorted([mod for mod in module_list if mod is not None]))
            )
        )
    return delimiter.join(buffer)

def _serialize_type(import_type_dict: Dict[str, Set[Optional[str]]], delimiter: str) -> str:
    """Serialize a given import type."""
    import_list = []
    for package_name in sorted(list(import_type_dict.keys())):
        module_list = import_type_dict[package_name]
        import_list.append(_serialize_package(package_name, module_list, delimiter))
    return delimiter.join(import_list)

def _get_import_clauses(imports: Dict[ImportType, Dict[str, Set[Optional[str]]]], delimiter: str) -> List[str]:
    import_clause = []
    for import_type in ImportType:
        if import_type in imports:
            import_clause.append(_serialize_type(imports[import_type], delimiter))
    return import_clause


class FileImportSerializer:
    def __init__(self, file_import: FileImport, is_python_3_file: bool) -> None:
        self._file_import = file_import
        self.is_python_3_file = is_python_3_file

    def _switch_typing_section_key(self, new_key: TypingSection):
        switched_dictionary = {}
        switched_dictionary[new_key] = self._file_import.imports[TypingSection.CONDITIONAL]
        return switched_dictionary

    def _get_imports_dict(self, baseline_typing_section: TypingSection, add_conditional_typing: bool):
        # If this is a python 3 file, our regular imports include the CONDITIONAL category
        # If this is not a python 3 file, our typing imports include the CONDITIONAL category
        file_import_copy = deepcopy(self._file_import)
        if add_conditional_typing and self._file_import.imports.get(TypingSection.CONDITIONAL):
            # we switch the TypingSection key for the CONDITIONAL typing imports so we can merge
            # the imports together
            switched_imports_dictionary = self._switch_typing_section_key(baseline_typing_section)
            switched_imports = FileImport(switched_imports_dictionary)
            file_import_copy.merge(switched_imports)
        return file_import_copy.imports.get(baseline_typing_section, {})

    def _add_type_checking_import(self):
        if (
            self._file_import.imports.get(TypingSection.TYPING) or
            (not self.is_python_3_file and self._file_import.imports.get(TypingSection.CONDITIONAL))
        ):
            self._file_import.add_from_import("typing", "TYPE_CHECKING", ImportType.STDLIB)

    def __str__(self) -> str:
        self._add_type_checking_import()
        regular_imports = ""
        regular_imports_dict = self._get_imports_dict(
            baseline_typing_section=TypingSection.REGULAR, add_conditional_typing=self.is_python_3_file
        )

        if regular_imports_dict:
            regular_imports = "\n\n".join(
                _get_import_clauses(regular_imports_dict, "\n")
            )

        typing_imports = ""
        typing_imports_dict = self._get_imports_dict(
            baseline_typing_section=TypingSection.TYPING, add_conditional_typing=not self.is_python_3_file
        )
        if typing_imports_dict:
            typing_imports += "\n\nif TYPE_CHECKING:\n    # pylint: disable=unused-import,ungrouped-imports\n    "
            typing_imports += "\n\n    ".join(_get_import_clauses(typing_imports_dict, "\n    "))

        return regular_imports + typing_imports
