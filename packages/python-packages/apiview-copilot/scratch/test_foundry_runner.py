"""
Test script for the Foundry runner using the resolve_package prompt.
"""

import os
import sys

# Add parent directory to path for imports
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from src._foundry_runner import parse_prompty_file, run_prompty_foundry


def test_parse_prompty():
    """Test that we can parse a prompty file correctly."""
    print("=" * 60)
    print("Testing prompty file parsing...")
    print("=" * 60)

    from src._utils import get_prompt_path

    prompt_path = get_prompt_path(folder="other", filename="resolve_package")

    config = parse_prompty_file(prompt_path)

    print(f"Name: {config.name}")
    print(f"Description: {config.description}")
    print(f"Model: {config.azure_deployment}")
    print(f"Temperature: {config.temperature}")
    print(f"Max tokens: {config.max_completion_tokens}")
    print(f"System template: {config.system_template[:100] if config.system_template else 'None'}...")
    print(f"User template: {config.user_template[:100] if config.user_template else 'None'}...")
    print("✓ Parsing successful!")
    print()
    return config


def test_run_prompty_foundry(model_override: str = None, max_tokens: int = None):
    """Test running a prompt via Azure AI Foundry."""
    print("=" * 60)
    print("Testing run_prompty_foundry...")
    print("=" * 60)

    inputs = {
        "package_query": "azure storage blobs",
        "language": "Python",
        "available_packages": [
            "azure-storage-blob",
            "azure-storage-queue",
            "azure-storage-file-share",
            "azure-core",
            "azure-identity",
        ],
    }

    print(f"Query: {inputs['package_query']}")
    print(f"Language: {inputs['language']}")
    print(f"Available packages: {inputs['available_packages']}")
    if model_override:
        print(f"Model override: {model_override}")
    if max_tokens:
        print(f"Max tokens: {max_tokens}")
    print()
    print("Calling Azure AI Foundry...")

    try:
        result = run_prompty_foundry(
            folder="other",
            filename="resolve_package",
            inputs=inputs,
            model=model_override,
            max_tokens=max_tokens,
        )
        print(f"✓ Result: {result}")
        print()

        # Validate result
        if result and result.strip() in inputs["available_packages"]:
            print("✓ Result is a valid package name!")
        elif result and result.strip() == "NO_MATCH":
            print("! Result is NO_MATCH (no suitable package found)")
        else:
            print(f"? Unexpected result format: {result}")

    except Exception as e:
        print(f"✗ Error: {e}")
        raise


if __name__ == "__main__":
    # First test parsing
    test_parse_prompty()

    # Get model from command line if provided
    model = sys.argv[1] if len(sys.argv) > 1 else None
    # Get max_tokens from command line if provided (second arg)
    max_tok = int(sys.argv[2]) if len(sys.argv) > 2 else None

    if not model:
        print("\nTip: Pass a model name as argument, e.g.:")
        print("  python scratch/test_foundry_runner.py DeepSeek-V3")
        print("  python scratch/test_foundry_runner.py gpt-4.1 16000")
        print()

    # Then test actual execution
    test_run_prompty_foundry(model_override=model, max_tokens=max_tok)
