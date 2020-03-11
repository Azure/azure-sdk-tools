import logging
import inspect
import astroid
from docstring_parser import parse
from docstring_parser.styles import Style

from _base_node import NodeEntityBase


logging.getLogger().setLevel(logging.INFO)


class FunctionNode(NodeEntityBase):
    """Function node to represent parsed function and method nodes
    """
    def __init__(self, obj):
        super().__init__(obj)
        self.annotations = []
        self._inspect()


    def _inspect(self):
        code = inspect.getsource(self.obj).strip()
        def_key = "async def" if code.startswith("async def") else "def"
        self.display_name = "{0} {1}{2}".format(def_key, self.display_name, self._get_signature())
        node = astroid.extract_node(inspect.getsource(self.obj))
        if node.decorators:
            self.annotations = ["@{}".format(x.name) for x in node.decorators.nodes if hasattr(x, "name")]

    def _get_args(self, parsed_docstring):
        params = []
        if parsed_docstring:
            for p in parsed_docstring.params:
                if p.type_name:
                    params.append("{0} : {1}".format(p.arg_name, p.type_name))
                else:
                    params.append(p.arg_name)

        if params:
            return "({})".format(", ".join(params))
        else:
            return str(inspect.signature(self.obj))


    def _get_signature(self):
        parsed_docstring = None
        if hasattr(self.obj, "__doc__"):
            parsed_docstring = parse(self.obj.__doc__, Style.rest)
        
        signature = self._get_args(parsed_docstring)

        # return part
        """if parsed_docstring:
            ret_type = parsed_docstring.returns
            if ret_type and ret_type.type_name:
                signature = "{0} --> {1}".format(signature, ret_type.type_name)
        """
        return signature


    def validate_signature(self):
        pass


    def dump(self, delim):
        space = ' '* delim
        for annot in self.annotations:
            print("{0}{1}".format(space, annot))
        print("{0}{1}".format(space, self.display_name))    
            


    def generate_tokens(self):
        pass