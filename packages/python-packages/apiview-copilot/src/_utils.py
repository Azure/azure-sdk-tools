# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module of utility functions for APIView Copilot.
"""

import os
from datetime import datetime, timezone


def get_language_pretty_name(language: str) -> str:
    """
    Returns a pretty name for the language.
    Args:
        language (str): The language to get the pretty name for.
    """
    language_pretty_names = {
        "android": "Android",
        "cpp": "C++",
        "dotnet": "C#",
        "golang": "Go",
        "ios": "Swift",
        "java": "Java",
        "python": "Python",
        "rust": "Rust",
        "typescript": "JavaScript",
    }
    pretty_name = language_pretty_names.get(language, language.capitalize())
    return pretty_name


def get_prompt_path(*, folder: str, filename: str) -> str:
    """
    Returns the full path to a prompt file.
    Args:
        folder (str): The folder containing the prompt.
        filename (str): The name of the prompt file.
    """
    # if filename doesn't end with .prompty, append it
    if not filename.endswith(".prompty"):
        filename += ".prompty"

    # Set up paths
    package_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    prompts_folder = os.path.join(package_root, "prompts")

    prompt_path = os.path.abspath(os.path.join(prompts_folder, folder, filename))
    if not os.path.exists(prompt_path):
        raise FileNotFoundError(f"Prompt file not found: {prompt_path}")
    return prompt_path


def to_epoch_seconds(date_str: str, *, end_of_day: bool = False) -> int:
    """
    Convert a date string to epoch seconds (UTC).

    Accepted inputs:
      - "YYYY-MM-DD"                -> treated as midnight UTC (or end of day if end_of_day=True)
      - full ISO-8601 datetime e.g. "2025-08-01T12:34:56Z" or "2025-08-01T12:34:56+00:00"

    Returns integer seconds since the epoch (UTC).

    Raises:
      ValueError if the input format cannot be parsed.
    """
    # Fast path for simple YYYY-MM-DD
    if len(date_str) == 10 and date_str.count("-") == 2:
        try:
            year, month, day = map(int, date_str.split("-"))
        except Exception as exc:
            raise ValueError(f"Invalid date: {date_str}") from exc
        if end_of_day:
            dt = datetime(year, month, day, 23, 59, 59, 999999, tzinfo=timezone.utc)
        else:
            dt = datetime(year, month, day, 0, 0, 0, 0, tzinfo=timezone.utc)
        return int(dt.timestamp())

    # Otherwise try ISO parsing
    try:
        # datetime.fromisoformat handles offsets like +00:00 but not trailing 'Z' in some versions.
        ds = date_str
        if ds.endswith("Z"):
            ds = ds[:-1] + "+00:00"
        dt = datetime.fromisoformat(ds)
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        else:
            dt = dt.astimezone(timezone.utc)
        return int(dt.timestamp())
    except Exception as exc:
        raise ValueError(f"Unrecognized date format: {date_str}") from exc


def to_iso8601(date_str: str, *, end_of_day: bool = False) -> str:
    """
    Convert a date string to ISO8601 format at midnight or end-of-day UTC.

    Accepted inputs:
      - "YYYY-MM-DD"                -> returns YYYY-MM-DDT00:00:00Z or YYYY-MM-DDT23:59:59.999Z
      - full ISO-8601 datetime e.g. "2025-08-01T12:34:56Z" or "2025-08-01T12:34:56+00:00"

    Returns ISO8601 string in UTC.
    """
    # datetime and timezone already imported at top
    # Fast path for simple YYYY-MM-DD
    if len(date_str) == 10 and date_str.count("-") == 2:
        year, month, day = map(int, date_str.split("-"))
        if end_of_day:
            dt = datetime(year, month, day, 23, 59, 59, 999999, tzinfo=timezone.utc)
        else:
            dt = datetime(year, month, day, 0, 0, 0, 0, tzinfo=timezone.utc)
        return dt.isoformat().replace("+00:00", "Z")
    # Otherwise try ISO parsing
    ds = date_str.replace("Z", "")
    try:
        dt = datetime.fromisoformat(ds)
    except Exception:
        dt = datetime.strptime(ds, "%Y-%m-%d")
    if end_of_day:
        dt = dt.replace(hour=23, minute=59, second=59, microsecond=999999, tzinfo=timezone.utc)
    else:
        dt = dt.replace(hour=0, minute=0, second=0, microsecond=0, tzinfo=timezone.utc)
    return dt.isoformat().replace("+00:00", "Z")
