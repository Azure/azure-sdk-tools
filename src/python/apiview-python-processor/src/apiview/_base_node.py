import logging
import inspect
import astroid
import docstring_parser

logging.getLogger().setLevel(logging.INFO)


class NodeEntityBase:
    def __init__(self, obj):
        super().__init__()
        self.obj = obj
        self.name = obj.__name__
        self.qualname = self.name
        if hasattr(obj, "__qualname__"):
            self.qualname = obj.__qualname__
        self.display_name = self.name        
        self.child_nodes = []
        
    
    def get_display_name(self):
        return self.display_name    

    def get_child_nodes(self):
        return child_nodes

    def get_name(self):
        return name

    def dump(self, delim = 0):
        if not self.child_nodes:
            return None
        print("{}\n".format(self.display_name))
        for n in self.child_nodes:
            n.dump(delim+5)
            """for annotation in n.annotations:
                print("         @{}".format(annotation))
            print("         {}\n".format(n.display_name))"""

