package test_output

import "github.com/Azure/azure-sdk-tools/src/go/cmd/testdata/test_output/subpackage"

type InterfaceA = subpackage.Interface

type Unimplementable = subpackage.Unimplementable

type StructA = subpackage.StructA

type StructB = subpackage.StructB

type StructEmpty = subpackage.StructEmpty

type Enum = subpackage.Enum

const EnumValue Enum = "EnumValue"

var EnumValue2 Enum = "EnumValue2"

func PossibleEnumValues() []Enum {
	return []Enum{EnumValue, EnumValue2}
}

type Enum2 struct{}

type Enum3 int

var (
	Enum2_1 *Enum2 = &Enum2{}
	Enum2_2 *Enum2 = &Enum2{}
)

func PossibleEnum2Values() []Enum2 {
	return []Enum2{*Enum2_1, *Enum2_2}
}

const String = subpackage.String

type doNotShowStruct struct{}

type doNotShowInterface interface{}

type doNotShowFunc func()

type doNotShowEnum string

const doNotShowEnumValue doNotShowEnum = "..."
