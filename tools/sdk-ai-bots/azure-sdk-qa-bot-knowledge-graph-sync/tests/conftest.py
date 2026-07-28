"""Shared test fixtures and configuration."""

import sys
from pathlib import Path

# Ensure the project root (which holds the package dir) is importable
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
