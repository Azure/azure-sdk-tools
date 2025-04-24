import json
from typing import Any, Union, List
import httpx
from pathlib import Path
import os
from pylint.lint import Run
from pylint.reporters import JSONReporter, text
import logging
import sys
import subprocess
from io import StringIO

from mcp.server.fastmcp import FastMCP

# Initialize FastMCP server
mcp = FastMCP("pylint")

logger = logging.getLogger()

# Create a StreamHandler to output to stderr
handler = logging.StreamHandler(sys.stderr)
handler.setLevel(logging.INFO)
# Add the handler to the logger
logger.addHandler(handler)
logger.setLevel(logging.INFO)

@mcp.tool()
def get_pylint(path: str) -> str:
    """Get pylint warnings for a given package or module.

    Args:
        path: Absolute path to the package or module to run pylint on.
        fast_mode: If True, run a faster limited lint checking only errors.
        file_only: If True and path is a file, only lint that specific file (not the whole package).

    Returns:
        JSON string containing pylint warnings or error message.
    """
    # Check if path exists
    if not Path(path).exists():
        return f"Error: Path {path} does not exist."
    
    # Get path to pylintrc
    # Look for pylintrc in the following locations:
    # 1. Current directory of the script
    script_dir = Path(__file__).parent.absolute()
    server_dir = script_dir.parent
    
    # Check multiple possible locations for .pylintrc
    pylintrc_locations = [
        Path.cwd() / '.pylintrc',               # Current working directory
        server_dir / '.pylintrc',               # The pylint-server directory
        Path(__file__).resolve().parent.parent / '.pylintrc',  # Parent directory of this script
    ]
    
    pylintrc_path = None
    for loc in pylintrc_locations:
        if loc.exists():
            pylintrc_path = str(loc)
            logger.info(f"Found pylintrc at: {pylintrc_path}")
            break
            
    if not pylintrc_path:
        logger.warning("No .pylintrc file found in expected locations. Using default pylint settings.")
    
    # Base command arguments
    cmd_args = [str(path), "--output-format=json:pylint_warnings.json"]
    
    if pylintrc_path:
        logger.info(f"Using pylintrc file: {pylintrc_path}")
        cmd_args.append(f'--rcfile={pylintrc_path}')
    
        
    # Run pylint with configuration
    try:
        logger.info(f"Running pylint for path: {path}")
        logger.info(f"Command: {cmd_args}")
        Run(
            cmd_args,
            exit=False,
        )
        
        logger.info(f"Command executed successfully.")

        # Read the output file
        try:
            with open('pylint_warnings.json', 'r') as f:
                pylint_warnings = json.load(f)
                return json.dumps(pylint_warnings)
        except (FileNotFoundError, json.JSONDecodeError) as e:
            logger.error(f"Error reading pylint output: {str(e)}")
            return json.dumps({"error": "Error reading pylint output"})

    except Exception as e:
        logger.error(f"Error running pylint: {str(e)}")
        return json.dumps({"error": f"Error running pylint: {str(e)}"})

if __name__ == "__main__":
    # Path to analyze
    mcp.run(transport='stdio')




















