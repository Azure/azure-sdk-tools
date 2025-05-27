from models import SearchGuidelinesInput, SearchGuidelinesOutput
from mcp.server.fastmcp import FastMCP

mcp = FastMCP(name="Guidelines Server")


@mcp.tool(description="Search for guidelines based on a query")
def search_guidelines_tool(input: SearchGuidelinesInput) -> SearchGuidelinesOutput:
    # Your business/search logic here
    # Here, just a demo response
    return SearchGuidelinesOutput(
        results=["Use 'async' suffix for async methods in Python.", "Return long-running operations as pollers."]
    )
