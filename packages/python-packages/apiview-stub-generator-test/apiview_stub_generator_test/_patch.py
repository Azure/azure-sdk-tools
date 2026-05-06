# coding=utf-8
# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# This file contains handwritten code that extends the generated models.
# --------------------------------------------------------------------------

from typing import overload, Optional, ClassVar, Dict, List, Union, TypedDict
from dataclasses import dataclass
from enum import Enum
from .models._models import (
    ClassWithIvarsAndCvars, 
    SomethingWithOverloads, 
)


@dataclass
class HandwrittenExtendedClass(ClassWithIvarsAndCvars, SomethingWithOverloads):
    """Handwritten class with multiple inheritance testing complex extension patterns.
    
    Tests: Multiple inheritance + extensive handwritten extensions (ivars, cvars, properties, overloaded methods, class decorator).
    """
    
    # New class variables (cvars)
    handwritten_class_var: ClassVar[str] = "handwritten_value"  # Tests: Simple cvar with handwritten render_classes
    
    # New instance variables (ivars) with type hints
    handwritten_name: str  # Tests: Simple ivar with handwritten render_classes
    
    def __init__(self, name: str = "DefaultHandwritten", damage: int = 0, custom_props: Optional[List[str]] = None):
        """Tests: Multi-line method signature (>2 params) with handwritten render_classes."""
        pass
    
    @property
    def handwritten_property(self) -> str:
        """Tests: Property getter with handwritten render_classes."""
        pass
    
    @overload
    def handwritten_process(self, data: str) -> str:
        """Tests: Overloaded method - all overload tokens should have handwritten render_classes."""
        ...
    
    @overload
    def handwritten_process(self, data: int) -> int:
        """Tests: Overloaded method variant."""
        ...
    
    def handwritten_process(self, data: Union[str, int]) -> Union[str, int]:
        """Tests: Overloaded method implementation with single-line signature (2 params)."""
        pass
    
    def get_summary(self, data: str, value: int, another: bool) -> Dict[str, str]:
        """Tests: Multi-line method signature."""
        pass
    
    @staticmethod
    def validate_data(data: Dict) -> bool:
        """Tests: Static method with single-line signature (1 param) and handwritten render_classes."""
        pass


class HandwrittenEnum(Enum):
    """Tests: Handwritten enum with handwritten render_classes."""
    VALUE_A = "a"
    VALUE_B = "b"


class HandwrittenDict(TypedDict):
    """Tests: Handwritten TypedDict with handwritten render_classes."""
    name: str
    value: int
