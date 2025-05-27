from models import SearchGuidelinesInput, SearchGuidelinesOutput
from src.mcp.tools import search_guidelines_tool
from mcp.server.fastmcp import FastMCP

mcp_app = FastMCP(tools=[search_guidelines_tool])
