package internal

type Widget struct {
	OK             bool
	Value          WidgetValue
	MissingScalar  WidgetProperties
	MissingScalarP *WidgetPropertiesP
	MissingSlice   []WidgetThings
	MissingSliceP  []*WidgetThingsP
}

type WidgetProperties struct{}

type WidgetPropertiesP struct{}

type WidgetThings struct{}

type WidgetThingsP struct{}

type WidgetValue struct{}
