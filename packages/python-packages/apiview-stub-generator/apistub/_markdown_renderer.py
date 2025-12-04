#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

"""
Markdown renderer for APIView tokens.
Converts ReviewLine tokens to markdown format with Python syntax highlighting.
"""


def render_markdown(apiview):
    """
    Convert ApiView ReviewLines to markdown format.
    
    Args:
        apiview: ApiView object containing review lines
    
    Returns:
        String containing markdown-formatted API review with:
        - Python code block with syntax highlighting
        - Rendered API surface from ReviewLines
    """
    lines = []
    
    # Start Python code block for syntax highlighting
    lines.append("```python")
    
    # Render all review lines with proper indentation
    def render_lines(review_lines, indent_level=0):
        """Recursively render review lines with proper indentation for hierarchy"""
        result = []
        indent = "    " * indent_level  # 4 spaces per indent level
        
        for line in review_lines:
            # Render tokens for this line (tokens already have their own spacing)
            line_text = "".join([token.render() for token in line.tokens])
            
            # Add hierarchical indentation if there's content
            if line_text.strip():
                result.append(indent + line_text)
            else:
                # Blank lines don't need indentation
                result.append("")
            
            # Recursively render children with increased indentation
            if line.children:
                result.extend(render_lines(line.children, indent_level + 1))
        
        return result
    
    rendered_lines = render_lines(apiview.review_lines)
    lines.extend(rendered_lines)
    
    # Close code block
    lines.append("```")
    
    return "\n".join(lines)
