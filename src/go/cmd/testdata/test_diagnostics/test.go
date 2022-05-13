package test_diagnostics

import (
	"net/http"
	"test_diagnostics/internal"
)

type unexportedStruct struct{}

type ExportedStruct struct {
	unexportedStruct
}

type Alias = internal.InternalStruct

type Sealed interface {
	foo()
}

type ExternalAlias = http.Client
