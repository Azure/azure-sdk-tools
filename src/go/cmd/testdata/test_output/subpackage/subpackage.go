package subpackage

type Interface interface {
	MethodNoReturn()
	MethodTwoReturns() (*string, error)
	MethodOneReturn() *string
}

type Unimplementable interface {
	unexported()
}

type StructEmpty struct {
	unexported string
}

func (s StructEmpty) MarshalJSON() ([]byte, error) {
	return []byte(s.unexported), nil
}

func (s *StructEmpty) UnmarshalJSON([]byte) error {
	return nil
}

type StructA struct {
	Exported       string
	notExported    string
	N              int
	ExportedAsWell string
}

func NewStructA() StructA {
	return StructA{notExported: "not exported"}
}

func NewStructAWithString(s string) StructA {
	return StructA{Exported: s}
}

func (StructA) MethodNoReturn() {}

func (s StructA) MethodOneReturn() string {
	return s.Exported
}

func (s *StructA) MethodTwoReturns() (string, error) {
	return s.Exported, nil
}

func (StructA) doNotShowMethod() {}

func (s StructA) MarshalJSON() ([]byte, error) {
	return nil, nil
}

func (s *StructA) UnmarshalJSON([]byte) error {
	return nil
}

type StructB struct {
	StructA
}

type StructC struct {
	Interface
	StructA
	Field StructB
}

func NewStructC(b StructB) StructC {
	return StructC{Field: b}
}

type StructGeneric[T any] struct {
	Value T
}

func NewStructGeneric[T any](t T) StructGeneric[T] {
	return StructGeneric[T]{Value: t}
}

func (s StructGeneric[int]) Method() (int, error) {
	return s.Value, nil
}

func GenericFunction[T any](t T) T {
	return t
}

func GenericFunctionTwoConstraints[T Interface, U any](t T, u U) (T, U) {
	return t, u
}

func Foo(a StructA) {}

func Bar() (string, error) {
	return "", nil
}

type Enum string

func (Enum) Method() {}

const (
	EnumA  Enum = "A"
	EnumB  Enum = "B"
	EnumCD Enum = "CD"
)

var (
	EnumC Enum = "C"
	EnumD Enum = "D"
)

func PossibleEnumValues() []Enum {
	return []Enum{EnumA, EnumB, EnumC, EnumD}
}

func NewEnum() Enum {
	return EnumA
}

func NewEnumPointer() *Enum {
	e := Enum("...")
	return &e
}

const String = "string"
