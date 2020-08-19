package testinterface

type SomeInterface interface {
	Foo(bar int64) *bool
}

type doNotShowInterface struct {
}

func (d *doNotShowInterface) Foo(bar string) *bool {
	return nil
}
