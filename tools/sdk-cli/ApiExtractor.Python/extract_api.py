#!/usr/bin/env python3
"""
Extract public API surface from Python packages.
Outputs JSON with classes, methods, functions, and their signatures.
"""

import ast
import json
import sys
import os
from pathlib import Path
from typing import Any

def get_docstring(node: ast.AST) -> str | None:
    """Extract first line of docstring."""
    doc = ast.get_docstring(node)
    if not doc:
        return None
    first_line = doc.split('\n')[0].strip()
    return first_line[:150] + '...' if len(first_line) > 150 else first_line

def format_annotation(ann: ast.expr | None) -> str | None:
    """Convert annotation AST to string."""
    if ann is None:
        return None
    return ast.unparse(ann)

def extract_function(node: ast.FunctionDef | ast.AsyncFunctionDef) -> dict[str, Any]:
    """Extract function/method info."""
    args = []
    for arg in node.args.args:
        arg_str = arg.arg
        if arg.annotation:
            arg_str += f": {ast.unparse(arg.annotation)}"
        args.append(arg_str)
    
    # Handle *args, **kwargs
    if node.args.vararg:
        va = node.args.vararg
        va_str = f"*{va.arg}"
        if va.annotation:
            va_str += f": {ast.unparse(va.annotation)}"
        args.append(va_str)
    if node.args.kwarg:
        kw = node.args.kwarg
        kw_str = f"**{kw.arg}"
        if kw.annotation:
            kw_str += f": {ast.unparse(kw.annotation)}"
        args.append(kw_str)
    
    sig = ", ".join(args)
    
    result: dict[str, Any] = {
        "name": node.name,
        "sig": sig,
    }
    
    ret = format_annotation(node.returns)
    if ret:
        result["ret"] = ret
    
    doc = get_docstring(node)
    if doc:
        result["doc"] = doc
    
    if isinstance(node, ast.AsyncFunctionDef):
        result["async"] = True
    
    # Check decorators
    for dec in node.decorator_list:
        if isinstance(dec, ast.Name):
            if dec.id == "classmethod":
                result["classmethod"] = True
            elif dec.id == "staticmethod":
                result["staticmethod"] = True
            elif dec.id == "property":
                result["property"] = True
    
    return result

def extract_class(node: ast.ClassDef) -> dict[str, Any]:
    """Extract class info."""
    bases = [ast.unparse(b) for b in node.bases if isinstance(b, (ast.Name, ast.Attribute, ast.Subscript))]
    
    result: dict[str, Any] = {
        "name": node.name,
    }
    
    if bases:
        result["base"] = ", ".join(bases)
    
    doc = get_docstring(node)
    if doc:
        result["doc"] = doc
    
    methods = []
    properties = []
    
    for item in node.body:
        if isinstance(item, (ast.FunctionDef, ast.AsyncFunctionDef)):
            # Skip private (single underscore) but keep dunder methods
            if item.name.startswith('_') and not item.name.startswith('__'):
                continue
            # Skip private dunders (double underscore not ending with double)
            if item.name.startswith('__') and not item.name.endswith('__'):
                continue
            
            func_info = extract_function(item)
            if func_info.get("property"):
                del func_info["property"]
                properties.append({"name": func_info["name"], "type": func_info.get("sig", "").split(" -> ")[-1] if " -> " in func_info.get("sig", "") else None, "doc": func_info.get("doc")})
            else:
                methods.append(func_info)
    
    if methods:
        result["methods"] = methods
    if properties:
        result["properties"] = properties
    
    return result

def extract_module(file_path: Path, root_path: Path) -> dict[str, Any]:
    """Extract module info."""
    try:
        code = file_path.read_text(encoding='utf-8')
        tree = ast.parse(code)
    except (SyntaxError, UnicodeDecodeError):
        return {}
    
    # Calculate module name
    rel_path = file_path.relative_to(root_path)
    module_name = str(rel_path).replace('/', '.').replace('\\', '.').replace('.py', '')
    if module_name.endswith('.__init__'):
        module_name = module_name[:-9]
    
    classes = []
    functions = []
    
    for node in ast.iter_child_nodes(tree):
        if isinstance(node, ast.ClassDef):
            if not node.name.startswith('_'):
                classes.append(extract_class(node))
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            if not node.name.startswith('_'):
                functions.append(extract_function(node))
    
    if not classes and not functions:
        return {}
    
    result: dict[str, Any] = {"name": module_name}
    if classes:
        result["classes"] = classes
    if functions:
        result["functions"] = functions
    
    return result

def find_package_name(root_path: Path) -> str:
    """Detect package name."""
    # Check pyproject.toml
    pyproject = root_path / "pyproject.toml"
    if pyproject.exists():
        import re
        content = pyproject.read_text()
        match = re.search(r'name\s*=\s*["\']([^"\']+)["\']', content)
        if match:
            return match.group(1)
    
    # Check setup.py
    setup_py = root_path / "setup.py"
    if setup_py.exists():
        import re
        content = setup_py.read_text()
        match = re.search(r'name\s*=\s*["\']([^"\']+)["\']', content)
        if match:
            return match.group(1)
    
    # Find first package with __init__.py
    for init in sorted(root_path.rglob("__init__.py"), key=lambda p: len(str(p))):
        if "test" not in str(init).lower() and "_generated" not in str(init):
            return init.parent.name
    
    return root_path.name

def extract_package(root_path: Path) -> dict[str, Any]:
    """Extract entire package API."""
    package_name = find_package_name(root_path)
    
    modules = []
    for py_file in sorted(root_path.rglob("*.py")):
        # Skip tests, caches, venvs
        path_str = str(py_file)
        if any(skip in path_str for skip in ['__pycache__', 'venv', '.venv', 'test_', '_test.py', '/tests/', '\\tests\\']):
            continue
        # Skip private modules except __init__
        if py_file.name.startswith('_') and py_file.name != '__init__.py':
            continue
        
        module = extract_module(py_file, root_path)
        if module:
            modules.append(module)
    
    return {
        "package": package_name,
        "modules": modules
    }

def format_python_stubs(api: dict[str, Any]) -> str:
    """Format as Python stub syntax."""
    lines = [
        f"# {api['package']} - Public API Surface",
        f"# Extracted by ApiExtractor.Python",
        "",
    ]
    
    for module in api.get("modules", []):
        lines.append(f"# Module: {module['name']}")
        lines.append("")
        
        for func in module.get("functions", []):
            if func.get("doc"):
                lines.append(f'"""{func["doc"]}"""')
            async_prefix = "async " if func.get("async") else ""
            ret_type = f' -> {func["ret"]}' if func.get("ret") else ""
            lines.append(f'{async_prefix}def {func["name"]}({func["sig"]}){ret_type}: ...')
            lines.append("")
        
        for cls in module.get("classes", []):
            base = f'({cls["base"]})' if cls.get("base") else ""
            lines.append(f'class {cls["name"]}{base}:')
            if cls.get("doc"):
                lines.append(f'    """{cls["doc"]}"""')
            
            for prop in cls.get("properties", []):
                type_hint = f": {prop['type']}" if prop.get("type") else ""
                lines.append(f'    {prop["name"]}{type_hint}')
            
            for method in cls.get("methods", []):
                if method.get("doc"):
                    lines.append(f'    """{method["doc"]}"""')
                decorators = []
                if method.get("classmethod"):
                    decorators.append("@classmethod")
                if method.get("staticmethod"):
                    decorators.append("@staticmethod")
                for dec in decorators:
                    lines.append(f'    {dec}')
                async_prefix = "async " if method.get("async") else ""
                ret_type = f' -> {method["ret"]}' if method.get("ret") else ""
                lines.append(f'    {async_prefix}def {method["name"]}({method["sig"]}){ret_type}: ...')
            
            if not cls.get("methods") and not cls.get("properties"):
                lines.append("    ...")
            lines.append("")
    
    return "\n".join(lines)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python extract_api.py <path> [--json] [--stub]", file=sys.stderr)
        sys.exit(1)
    
    root = Path(sys.argv[1]).resolve()
    output_json = "--json" in sys.argv
    output_stub = "--stub" in sys.argv or not output_json
    
    api = extract_package(root)
    
    if output_json:
        print(json.dumps(api, indent=2))
    elif output_stub:
        print(format_python_stubs(api))
