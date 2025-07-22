from typing import List, Union, TYPE_CHECKING
import astroid
from .nodes._function_node import FunctionNode

if TYPE_CHECKING:
    from .nodes._module_node import ModuleNode
    from .nodes._class_node import ClassNode


def parse_overloads(
    node: Union["ModuleNode", "ClassNode"],
    functions: List[astroid.FunctionDef],
    *,
    is_module_level: bool = False,
) -> List[FunctionNode]:
    """Uses AST parsing to look for @overload decorated functions
    because inspect cannot see these. Returns a list of overloads given class or module node and function nodes to look through.
    """
    overload_nodes = []
    for func in functions:
        if not func.decorators:
            continue
        for ast_dec in func.decorators.nodes:
            try:
                if ast_dec.name == "overload":
                    overload_node = FunctionNode(
                        node.namespace,
                        node,
                        node=func,
                        is_module_level=is_module_level,
                        apiview=node.apiview,
                    )
                    overload_nodes.append(overload_node)
            except AttributeError:
                continue
    return overload_nodes


def add_overload_nodes(
    node: Union["ModuleNode", "ClassNode"],
    func_node: FunctionNode,
    overloads: List[FunctionNode],
) -> None:
    """Gets overloads of the function node and appends them to the child nodes, then appends the function node."""
    func_overloads = [x for x in overloads if x.name == func_node.name]

    # Append a numeric tag to overloads to distinguish them from one another.
    # This will break down if overloads are moved around in the source file.
    for x, overload in enumerate(func_overloads):
        overload.namespace_id = overload.namespace_id + f"_{x+1}"
        overload.is_handwritten = func_node.is_handwritten
        node.child_nodes.append(overload)
    node.child_nodes.append(func_node)
