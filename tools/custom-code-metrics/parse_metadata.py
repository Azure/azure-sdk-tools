"""Decode committed package metadata without importing or executing package code."""
import ast
import csv
import io
import json
import sys
import tomllib
import xml.etree.ElementTree as ET


def setup_metadata(text):
    tree = ast.parse(text)
    bindings = {}
    for node in tree.body:
        if isinstance(node, ast.Assign):
            for target in node.targets:
                if isinstance(target, ast.Name):
                    bindings[target.id] = node.value

    def value(node, seen=frozenset()):
        if isinstance(node, ast.Constant):
            return node.value
        if isinstance(node, ast.Name) and node.id in bindings and node.id not in seen:
            return value(bindings[node.id], seen | {node.id})
        if isinstance(node, (ast.List, ast.Tuple)):
            return [value(item, seen) for item in node.elts]
        if isinstance(node, ast.Dict):
            return {value(key, seen): value(item, seen) for key, item in zip(node.keys, node.values)}
        if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Add):
            return value(node.left, seen) + value(node.right, seen)
        if isinstance(node, ast.Call):
            name = node.func.id if isinstance(node.func, ast.Name) else getattr(node.func, "attr", "")
            if name in ("find_packages", "find_namespace_packages"):
                if node.args:
                    raise ValueError("Positional package discovery arguments are not supported.")
                return {"find": {**{arg.arg: value(arg.value, seen) for arg in node.keywords},
                                 "namespaces": name == "find_namespace_packages"}}
        raise ValueError(f"Unsupported static setup expression: {ast.dump(node, include_attributes=False)}")

    calls = [node for node in ast.walk(tree) if isinstance(node, ast.Call)
             and (getattr(node.func, "id", "") == "setup" or getattr(node.func, "attr", "") == "setup")]
    if len(calls) != 1:
        raise ValueError("Exactly one static setup declaration is required.")
    fields = {"name", "packages", "package_dir", "package_data", "exclude_package_data", "py_modules"}
    result = {}
    for arg in calls[0].keywords:
        if arg.arg in fields:
            try:
                result[arg.arg] = value(arg.value)
            except ValueError as error:
                if arg.arg == "name":
                    raise
                result[arg.arg + "_error"] = str(error)
        elif arg.arg is None:
            raise ValueError("Dynamic setup keyword expansion is not supported.")
    if not isinstance(result.get("name"), str):
        raise ValueError("A static distribution name is required.")
    result["sdist_only"] = any(
        isinstance(node, ast.If)
        and isinstance(node.test, ast.Compare)
        and isinstance(node.test.left, ast.Constant)
        and node.test.left.value == "sdist"
        and len(node.test.ops) == 1 and isinstance(node.test.ops[0], ast.In)
        and len(node.test.comparators) == 1
        and isinstance(node.test.comparators[0], ast.Attribute)
        and isinstance(node.test.comparators[0].value, ast.Name)
        and node.test.comparators[0].value.id == "sys"
        and node.test.comparators[0].attr == "argv"
        and any(isinstance(branch, ast.Raise) and isinstance(branch.exc, ast.Call)
                and getattr(branch.exc.func, "id", "") == "RuntimeError" for branch in node.orelse)
        and any(call in list(ast.walk(branch)) for branch in node.body for call in calls)
        for node in tree.body
    )
    return result


def xml_node(node):
    return {"name": node.tag.split("}")[-1], "text": (node.text or "").strip(),
            "attributes": dict(node.attrib), "children": [xml_node(child) for child in node]}


def decode(item):
    kind, text = item["kind"], item["text"]
    if kind == "toml":
        return tomllib.loads(text)
    if kind == "setup":
        return setup_metadata(text)
    if kind == "xml":
        if "<!DOCTYPE" in text.upper() or "<!ENTITY" in text.upper():
            raise ValueError("DTD/entity declarations are not supported in package metadata.")
        return xml_node(ET.fromstring(text))
    if kind == "csv":
        return list(csv.DictReader(io.StringIO(text)))
    raise ValueError(f"Unsupported metadata format: {kind}")


def main():
    records = json.load(sys.stdin)
    output = []
    for item in records:
        try:
            output.append({"path": item["path"], "value": decode(item)})
        except (ValueError, SyntaxError, ET.ParseError, tomllib.TOMLDecodeError) as error:
            raise ValueError(f"{item['path']}: {error}") from error
    json.dump(output, sys.stdout, separators=(",", ":"))


if __name__ == "__main__":
    main()
