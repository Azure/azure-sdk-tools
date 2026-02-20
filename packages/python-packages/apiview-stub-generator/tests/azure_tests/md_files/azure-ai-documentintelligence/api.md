```py
# Package is parsed using apiview-stub-generator(version:0.3.27), Python version: 3.10.12


namespace azure.ai.documentintelligence

    class azure.ai.documentintelligence.AnalyzeDocumentLROPoller(LROPoller[+PollingReturnType_co]):
        property details: Mapping[str, Any]    # Read-only

        @classmethod
        def from_continuation_token(
                cls, 
                polling_method: PollingMethod[PollingReturnType_co], 
                continuation_token: str, 
                **kwargs: Any
            ) -> AnalyzeDocumentLROPoller: ...


    class azure.ai.documentintelligence.DocumentIntelligenceAdministrationClient(DocumentIntelligenceAdministrationClient): implements ContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, TokenCredential], 
                *, 
                api_version: str = ..., 
                polling_interval: Optional[int] = ..., 
                **kwargs: Any
            ) -> None: ...

        @overload
        def authorize_classifier_copy(
                self, 
                body: AuthorizeClassifierCopyRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        def authorize_classifier_copy(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        def authorize_classifier_copy(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        def authorize_model_copy(
                self, 
                body: AuthorizeCopyRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        def authorize_model_copy(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        def authorize_model_copy(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        def begin_build_classifier(
                self, 
                body: BuildDocumentClassifierRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_build_classifier(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_build_classifier(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_build_document_model(
                self, 
                body: BuildDocumentModelRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_build_document_model(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_build_document_model(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_compose_model(
                self, 
                body: ComposeDocumentModelRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_compose_model(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_compose_model(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: ClassifierCopyAuthorization, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentClassifierDetails]: ...

        @overload
        def begin_copy_model_to(
                self, 
                model_id: str, 
                body: ModelCopyAuthorization, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_copy_model_to(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        @overload
        def begin_copy_model_to(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> LROPoller[DocumentModelDetails]: ...

        def close(self) -> None: ...

        @distributed_trace
        def delete_classifier(
                self, 
                classifier_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace
        def delete_model(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace
        def get_classifier(
                self, 
                classifier_id: str, 
                **kwargs: Any
            ) -> DocumentClassifierDetails: ...

        @distributed_trace
        def get_model(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> DocumentModelDetails: ...

        @distributed_trace
        def get_operation(
                self, 
                operation_id: str, 
                **kwargs: Any
            ) -> DocumentIntelligenceOperationDetails: ...

        @distributed_trace
        def get_resource_details(self, **kwargs: Any) -> DocumentIntelligenceResourceDetails: ...

        @distributed_trace
        def list_classifiers(self, **kwargs: Any) -> Iterable[DocumentClassifierDetails]: ...

        @distributed_trace
        def list_models(self, **kwargs: Any) -> Iterable[DocumentModelDetails]: ...

        @distributed_trace
        def list_operations(self, **kwargs: Any) -> Iterable[DocumentIntelligenceOperationDetails]: ...

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> HttpResponse: ...


    class azure.ai.documentintelligence.DocumentIntelligenceClient(DocumentIntelligenceClient): implements ContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, TokenCredential], 
                *, 
                api_version: str = ..., 
                polling_interval: Optional[int] = ..., 
                **kwargs: Any
            ) -> None: ...

        @overload
        def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: AnalyzeBatchDocumentsRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeBatchResult]: ...

        @overload
        def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeBatchResult]: ...

        @overload
        def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeBatchResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: AnalyzeDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: AnalyzeDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        @overload
        def begin_analyze_document(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        @overload
        def begin_classify_document(
                self, 
                classifier_id: str, 
                body: ClassifyDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        @overload
        def begin_classify_document(
                self, 
                classifier_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        @overload
        def begin_classify_document(
                self, 
                classifier_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> LROPoller[AnalyzeResult]: ...

        def close(self) -> None: ...

        @distributed_trace
        def delete_analyze_batch_result(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace
        def delete_analyze_result(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace
        def get_analyze_batch_result(self, continuation_token: str) -> LROPoller[AnalyzeBatchResult]: ...

        @distributed_trace
        def get_analyze_result_figure(
                self, 
                model_id: str, 
                result_id: str, 
                figure_id: str, 
                **kwargs: Any
            ) -> Iterator[bytes]: ...

        @distributed_trace
        def get_analyze_result_pdf(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> Iterator[bytes]: ...

        @distributed_trace
        def list_analyze_batch_results(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> Iterable[AnalyzeBatchOperation]: ...

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> HttpResponse: ...


namespace azure.ai.documentintelligence.aio

    class azure.ai.documentintelligence.aio.AsyncAnalyzeDocumentLROPoller(AsyncLROPoller[+PollingReturnType_co]):
        property details: Mapping[str, Any]    # Read-only

        @classmethod
        def from_continuation_token(
                cls, 
                polling_method: AsyncPollingMethod[PollingReturnType_co], 
                continuation_token: str, 
                **kwargs: Any
            ) -> AsyncAnalyzeDocumentLROPoller: ...


    class azure.ai.documentintelligence.aio.DocumentIntelligenceAdministrationClient(DocumentIntelligenceAdministrationClient): implements AsyncContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, AsyncTokenCredential], 
                *, 
                api_version: str = ..., 
                **kwargs: Any
            ) -> None: ...

        @overload
        async def authorize_classifier_copy(
                self, 
                body: AuthorizeClassifierCopyRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        async def authorize_classifier_copy(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        async def authorize_classifier_copy(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ClassifierCopyAuthorization: ...

        @overload
        async def authorize_model_copy(
                self, 
                body: AuthorizeCopyRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        async def authorize_model_copy(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        async def authorize_model_copy(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> ModelCopyAuthorization: ...

        @overload
        async def begin_build_classifier(
                self, 
                body: BuildDocumentClassifierRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_build_classifier(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_build_classifier(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_build_document_model(
                self, 
                body: BuildDocumentModelRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_build_document_model(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_build_document_model(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_compose_model(
                self, 
                body: ComposeDocumentModelRequest, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_compose_model(
                self, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_compose_model(
                self, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: ClassifierCopyAuthorization, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_copy_classifier_to(
                self, 
                classifier_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentClassifierDetails]: ...

        @overload
        async def begin_copy_model_to(
                self, 
                model_id: str, 
                body: ModelCopyAuthorization, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_copy_model_to(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        @overload
        async def begin_copy_model_to(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                **kwargs: Any
            ) -> AsyncLROPoller[DocumentModelDetails]: ...

        async def close(self) -> None: ...

        @distributed_trace_async
        async def delete_classifier(
                self, 
                classifier_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace_async
        async def delete_model(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace_async
        async def get_classifier(
                self, 
                classifier_id: str, 
                **kwargs: Any
            ) -> DocumentClassifierDetails: ...

        @distributed_trace_async
        async def get_model(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> DocumentModelDetails: ...

        @distributed_trace_async
        async def get_operation(
                self, 
                operation_id: str, 
                **kwargs: Any
            ) -> DocumentIntelligenceOperationDetails: ...

        @distributed_trace_async
        async def get_resource_details(self, **kwargs: Any) -> DocumentIntelligenceResourceDetails: ...

        @distributed_trace
        def list_classifiers(self, **kwargs: Any) -> AsyncIterable[DocumentClassifierDetails]: ...

        @distributed_trace
        def list_models(self, **kwargs: Any) -> AsyncIterable[DocumentModelDetails]: ...

        @distributed_trace
        def list_operations(self, **kwargs: Any) -> AsyncIterable[DocumentIntelligenceOperationDetails]: ...

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> Awaitable[AsyncHttpResponse]: ...


    class azure.ai.documentintelligence.aio.DocumentIntelligenceClient(DocumentIntelligenceClient): implements AsyncContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, AsyncTokenCredential], 
                *, 
                api_version: str = ..., 
                **kwargs: Any
            ) -> None: ...

        @overload
        async def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: AnalyzeBatchDocumentsRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeBatchResult]: ...

        @overload
        async def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeBatchResult]: ...

        @overload
        async def begin_analyze_batch_documents(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeBatchResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: AnalyzeDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncAnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncAnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncAnalyzeDocumentLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: AnalyzeDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_analyze_document(
                self, 
                model_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                locale: Optional[str] = ..., 
                output: Optional[List[Union[str, AnalyzeOutputOption]]] = ..., 
                output_content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                pages: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_classify_document(
                self, 
                classifier_id: str, 
                body: ClassifyDocumentRequest, 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_classify_document(
                self, 
                classifier_id: str, 
                body: JSON, 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        @overload
        async def begin_classify_document(
                self, 
                classifier_id: str, 
                body: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                pages: Optional[str] = ..., 
                split: Optional[Union[str, SplitMode]] = ..., 
                string_index_type: Optional[Union[str, StringIndexType]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[AnalyzeResult]: ...

        async def close(self) -> None: ...

        @distributed_trace_async
        async def delete_analyze_batch_result(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace_async
        async def delete_analyze_result(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> None: ...

        @distributed_trace_async
        async def get_analyze_batch_result(self, continuation_token: str) -> AsyncLROPoller[AnalyzeBatchResult]: ...

        @distributed_trace_async
        async def get_analyze_result_figure(
                self, 
                model_id: str, 
                result_id: str, 
                figure_id: str, 
                **kwargs: Any
            ) -> AsyncIterator[bytes]: ...

        @distributed_trace_async
        async def get_analyze_result_pdf(
                self, 
                model_id: str, 
                result_id: str, 
                **kwargs: Any
            ) -> AsyncIterator[bytes]: ...

        @distributed_trace
        def list_analyze_batch_results(
                self, 
                model_id: str, 
                **kwargs: Any
            ) -> AsyncIterable[AnalyzeBatchOperation]: ...

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> Awaitable[AsyncHttpResponse]: ...


namespace azure.ai.documentintelligence.models

    class azure.ai.documentintelligence.models.AddressValue(MutableMapping[str, Any]):
        city: Optional[str]
        city_district: Optional[str]
        country_region: Optional[str]
        house: Optional[str]
        house_number: Optional[str]
        level: Optional[str]
        po_box: Optional[str]
        postal_code: Optional[str]
        road: Optional[str]
        state: Optional[str]
        state_district: Optional[str]
        street_address: Optional[str]
        suburb: Optional[str]
        unit: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                city: Optional[str] = ..., 
                city_district: Optional[str] = ..., 
                country_region: Optional[str] = ..., 
                house: Optional[str] = ..., 
                house_number: Optional[str] = ..., 
                level: Optional[str] = ..., 
                po_box: Optional[str] = ..., 
                postal_code: Optional[str] = ..., 
                road: Optional[str] = ..., 
                state: Optional[str] = ..., 
                state_district: Optional[str] = ..., 
                street_address: Optional[str] = ..., 
                suburb: Optional[str] = ..., 
                unit: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeBatchDocumentsRequest(MutableMapping[str, Any]):
        azure_blob_file_list_source: Optional[AzureBlobFileListContentSource]
        azure_blob_source: Optional[AzureBlobContentSource]
        overwrite_existing: Optional[bool]
        result_container_url: str
        result_prefix: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                azure_blob_file_list_source: Optional[AzureBlobFileListContentSource] = ..., 
                azure_blob_source: Optional[AzureBlobContentSource] = ..., 
                overwrite_existing: Optional[bool] = ..., 
                result_container_url: str, 
                result_prefix: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeBatchOperation(MutableMapping[str, Any]):
        created_date_time: datetime
        error: Optional[DocumentIntelligenceError]
        last_updated_date_time: datetime
        percent_completed: Optional[int]
        result: Optional[AnalyzeBatchResult]
        result_id: Optional[str]
        status: Union[str, DocumentIntelligenceOperationStatus]

        @overload
        def __init__(
                self, 
                *, 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                percent_completed: Optional[int] = ..., 
                result: Optional[AnalyzeBatchResult] = ..., 
                result_id: Optional[str] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeBatchOperationDetail(MutableMapping[str, Any]):
        error: Optional[DocumentIntelligenceError]
        result_url: Optional[str]
        source_url: str
        status: Union[str, DocumentIntelligenceOperationStatus]

        @overload
        def __init__(
                self, 
                *, 
                error: Optional[DocumentIntelligenceError] = ..., 
                result_url: Optional[str] = ..., 
                source_url: str, 
                status: Union[str, DocumentIntelligenceOperationStatus]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeBatchResult(MutableMapping[str, Any]):
        details: Optional[List[ForwardRef('AnalyzeBatchOperationDetail')]]
        failed_count: int
        skipped_count: int
        succeeded_count: int

        @overload
        def __init__(
                self, 
                *, 
                details: Optional[List[AnalyzeBatchOperationDetail]] = ..., 
                failed_count: int, 
                skipped_count: int, 
                succeeded_count: int
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeDocumentRequest(MutableMapping[str, Any]):
        bytes_source: Optional[bytes]
        url_source: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                bytes_source: Optional[bytes] = ..., 
                url_source: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzeOutputOption(str, Enum):
        FIGURES = "figures"
        PDF = "pdf"


    class azure.ai.documentintelligence.models.AnalyzeResult(MutableMapping[str, Any]):
        api_version: str
        content: str
        content_format: Optional[Union[str, DocumentContentFormat]]
        documents: Optional[List[ForwardRef('AnalyzedDocument')]]
        figures: Optional[List[ForwardRef('DocumentFigure')]]
        key_value_pairs: Optional[List[ForwardRef('DocumentKeyValuePair')]]
        languages: Optional[List[ForwardRef('DocumentLanguage')]]
        model_id: str
        pages: List[DocumentPage]
        paragraphs: Optional[List[ForwardRef('DocumentParagraph')]]
        sections: Optional[List[ForwardRef('DocumentSection')]]
        string_index_type: Union[str, StringIndexType]
        styles: Optional[List[ForwardRef('DocumentStyle')]]
        tables: Optional[List[ForwardRef('DocumentTable')]]
        warnings: Optional[List[ForwardRef('DocumentIntelligenceWarning')]]

        @overload
        def __init__(
                self, 
                *, 
                api_version: str, 
                content: str, 
                content_format: Optional[Union[str, DocumentContentFormat]] = ..., 
                documents: Optional[List[AnalyzedDocument]] = ..., 
                figures: Optional[List[DocumentFigure]] = ..., 
                key_value_pairs: Optional[List[DocumentKeyValuePair]] = ..., 
                languages: Optional[List[DocumentLanguage]] = ..., 
                model_id: str, 
                pages: List[DocumentPage], 
                paragraphs: Optional[List[DocumentParagraph]] = ..., 
                sections: Optional[List[DocumentSection]] = ..., 
                string_index_type: Union[str, StringIndexType], 
                styles: Optional[List[DocumentStyle]] = ..., 
                tables: Optional[List[DocumentTable]] = ..., 
                warnings: Optional[List[DocumentIntelligenceWarning]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AnalyzedDocument(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        confidence: float
        doc_type: str
        fields: Optional[Dict[str, ForwardRef('DocumentField')]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                confidence: float, 
                doc_type: str, 
                fields: Optional[Dict[str, DocumentField]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AuthorizeClassifierCopyRequest(MutableMapping[str, Any]):
        classifier_id: str
        description: Optional[str]
        tags: Optional[Dict[str, str]]

        @overload
        def __init__(
                self, 
                *, 
                classifier_id: str, 
                description: Optional[str] = ..., 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AuthorizeCopyRequest(MutableMapping[str, Any]):
        description: Optional[str]
        model_id: str
        tags: Optional[Dict[str, str]]

        @overload
        def __init__(
                self, 
                *, 
                description: Optional[str] = ..., 
                model_id: str, 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AzureBlobContentSource(MutableMapping[str, Any]):
        container_url: str
        prefix: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                container_url: str, 
                prefix: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.AzureBlobFileListContentSource(MutableMapping[str, Any]):
        container_url: str
        file_list: str

        @overload
        def __init__(
                self, 
                *, 
                container_url: str, 
                file_list: str
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.BoundingRegion(MutableMapping[str, Any]):
        page_number: int
        polygon: List[float]

        @overload
        def __init__(
                self, 
                *, 
                page_number: int, 
                polygon: List[float]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.BuildDocumentClassifierRequest(MutableMapping[str, Any]):
        allow_overwrite: Optional[bool]
        base_classifier_id: Optional[str]
        classifier_id: str
        description: Optional[str]
        doc_types: Dict[str, ClassifierDocumentTypeDetails]

        @overload
        def __init__(
                self, 
                *, 
                allow_overwrite: Optional[bool] = ..., 
                base_classifier_id: Optional[str] = ..., 
                classifier_id: str, 
                description: Optional[str] = ..., 
                doc_types: Dict[str, ClassifierDocumentTypeDetails]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.BuildDocumentModelRequest(MutableMapping[str, Any]):
        allow_overwrite: Optional[bool]
        azure_blob_file_list_source: Optional[AzureBlobFileListContentSource]
        azure_blob_source: Optional[AzureBlobContentSource]
        build_mode: Union[str, DocumentBuildMode]
        description: Optional[str]
        max_training_hours: Optional[float]
        model_id: str
        tags: Optional[Dict[str, str]]

        @overload
        def __init__(
                self, 
                *, 
                allow_overwrite: Optional[bool] = ..., 
                azure_blob_file_list_source: Optional[AzureBlobFileListContentSource] = ..., 
                azure_blob_source: Optional[AzureBlobContentSource] = ..., 
                build_mode: Union[str, DocumentBuildMode], 
                description: Optional[str] = ..., 
                max_training_hours: Optional[float] = ..., 
                model_id: str, 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.ClassifierCopyAuthorization(MutableMapping[str, Any]):
        access_token: str
        expiration_date_time: datetime
        target_classifier_id: str
        target_classifier_location: str
        target_resource_id: str
        target_resource_region: str

        @overload
        def __init__(
                self, 
                *, 
                access_token: str, 
                expiration_date_time: datetime, 
                target_classifier_id: str, 
                target_classifier_location: str, 
                target_resource_id: str, 
                target_resource_region: str
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.ClassifierDocumentTypeDetails(MutableMapping[str, Any]):
        azure_blob_file_list_source: Optional[AzureBlobFileListContentSource]
        azure_blob_source: Optional[AzureBlobContentSource]
        source_kind: Optional[Union[str, ContentSourceKind]]

        @overload
        def __init__(
                self, 
                *, 
                azure_blob_file_list_source: Optional[AzureBlobFileListContentSource] = ..., 
                azure_blob_source: Optional[AzureBlobContentSource] = ..., 
                source_kind: Optional[Union[str, ContentSourceKind]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.ClassifyDocumentRequest(MutableMapping[str, Any]):
        bytes_source: Optional[bytes]
        url_source: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                bytes_source: Optional[bytes] = ..., 
                url_source: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.ComposeDocumentModelRequest(MutableMapping[str, Any]):
        classifier_id: str
        description: Optional[str]
        doc_types: Dict[str, DocumentTypeDetails]
        model_id: str
        split: Optional[Union[str, SplitMode]]
        tags: Optional[Dict[str, str]]

        @overload
        def __init__(
                self, 
                *, 
                classifier_id: str, 
                description: Optional[str] = ..., 
                doc_types: Dict[str, DocumentTypeDetails], 
                model_id: str, 
                split: Optional[Union[str, SplitMode]] = ..., 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.ContentSourceKind(str, Enum):
        AZURE_BLOB = "azureBlob"
        AZURE_BLOB_FILE_LIST = "azureBlobFileList"
        BASE64 = "base64"
        URL = "url"


    class azure.ai.documentintelligence.models.CurrencyValue(MutableMapping[str, Any]):
        amount: float
        currency_code: Optional[str]
        currency_symbol: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                amount: float, 
                currency_code: Optional[str] = ..., 
                currency_symbol: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.CustomDocumentModelsDetails(MutableMapping[str, Any]):
        count: int
        limit: int

        @overload
        def __init__(
                self, 
                *, 
                count: int, 
                limit: int
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentAnalysisFeature(str, Enum):
        BARCODES = "barcodes"
        FORMULAS = "formulas"
        KEY_VALUE_PAIRS = "keyValuePairs"
        LANGUAGES = "languages"
        OCR_HIGH_RESOLUTION = "ocrHighResolution"
        QUERY_FIELDS = "queryFields"
        STYLE_FONT = "styleFont"


    class azure.ai.documentintelligence.models.DocumentBarcode(MutableMapping[str, Any]):
        confidence: float
        kind: Union[str, DocumentBarcodeKind]
        polygon: Optional[List[float]]
        span: DocumentSpan
        value: str

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                kind: Union[str, DocumentBarcodeKind], 
                polygon: Optional[List[float]] = ..., 
                span: DocumentSpan, 
                value: str
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentBarcodeKind(str, Enum):
        AZTEC = "Aztec"
        CODABAR = "Codabar"
        CODE128 = "Code128"
        CODE39 = "Code39"
        CODE93 = "Code93"
        DATA_BAR = "DataBar"
        DATA_BAR_EXPANDED = "DataBarExpanded"
        DATA_MATRIX = "DataMatrix"
        EAN13 = "EAN13"
        EAN8 = "EAN8"
        ITF = "ITF"
        MAXI_CODE = "MaxiCode"
        MICRO_QR_CODE = "MicroQRCode"
        PDF417 = "PDF417"
        QR_CODE = "QRCode"
        UPCA = "UPCA"
        UPCE = "UPCE"


    class azure.ai.documentintelligence.models.DocumentBuildMode(str, Enum):
        NEURAL = "neural"
        TEMPLATE = "template"


    class azure.ai.documentintelligence.models.DocumentCaption(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        content: str
        elements: Optional[List[str]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                content: str, 
                elements: Optional[List[str]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentClassifierBuildOperationDetails(MutableMapping[str, Any]):
        api_version: str
        created_date_time: datetime
        error: DocumentIntelligenceError
        kind: Literal[OperationKind.DOCUMENT_CLASSIFIER_BUILD]
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: int
        resource_location: str
        result: Optional[DocumentClassifierDetails]
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: dict[str, str]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                result: Optional[DocumentClassifierDetails] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentClassifierCopyToOperationDetails(MutableMapping[str, Any]):
        api_version: str
        created_date_time: datetime
        error: DocumentIntelligenceError
        kind: Literal[OperationKind.DOCUMENT_CLASSIFIER_COPY_TO]
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: int
        resource_location: str
        result: Optional[DocumentClassifierDetails]
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: dict[str, str]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                result: Optional[DocumentClassifierDetails] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentClassifierDetails(MutableMapping[str, Any]):
        api_version: str
        base_classifier_id: Optional[str]
        classifier_id: str
        created_date_time: datetime
        description: Optional[str]
        doc_types: Dict[str, ClassifierDocumentTypeDetails]
        expiration_date_time: Optional[datetime]
        modified_date_time: Optional[datetime]
        warnings: Optional[List[ForwardRef('DocumentIntelligenceWarning')]]

        @overload
        def __init__(
                self, 
                *, 
                api_version: str, 
                base_classifier_id: Optional[str] = ..., 
                classifier_id: str, 
                created_date_time: datetime, 
                description: Optional[str] = ..., 
                doc_types: Dict[str, ClassifierDocumentTypeDetails], 
                expiration_date_time: Optional[datetime] = ..., 
                warnings: Optional[List[DocumentIntelligenceWarning]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentContentFormat(str, Enum):
        MARKDOWN = "markdown"
        TEXT = "text"


    class azure.ai.documentintelligence.models.DocumentField(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        confidence: Optional[float]
        content: Optional[str]
        spans: Optional[List[ForwardRef('DocumentSpan')]]
        type: Union[str, DocumentFieldType]
        value_address: Optional[AddressValue]
        value_array: Optional[List[ForwardRef('DocumentField')]]
        value_boolean: Optional[bool]
        value_country_region: Optional[str]
        value_currency: Optional[CurrencyValue]
        value_date: Optional[date]
        value_integer: Optional[int]
        value_number: Optional[float]
        value_object: Optional[Dict[str, ForwardRef('DocumentField')]]
        value_phone_number: Optional[str]
        value_selection_group: Optional[List[str]]
        value_selection_mark: Optional[Union[str, DocumentSelectionMarkState]]
        value_signature: Optional[Union[str, DocumentSignatureType]]
        value_string: Optional[str]
        value_time: Optional[time]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                confidence: Optional[float] = ..., 
                content: Optional[str] = ..., 
                spans: Optional[List[DocumentSpan]] = ..., 
                type: Union[str, DocumentFieldType], 
                value_address: Optional[AddressValue] = ..., 
                value_array: Optional[List[DocumentField]] = ..., 
                value_boolean: Optional[bool] = ..., 
                value_country_region: Optional[str] = ..., 
                value_currency: Optional[CurrencyValue] = ..., 
                value_date: Optional[date] = ..., 
                value_integer: Optional[int] = ..., 
                value_number: Optional[float] = ..., 
                value_object: Optional[Dict[str, DocumentField]] = ..., 
                value_phone_number: Optional[str] = ..., 
                value_selection_group: Optional[List[str]] = ..., 
                value_selection_mark: Optional[Union[str, DocumentSelectionMarkState]] = ..., 
                value_signature: Optional[Union[str, DocumentSignatureType]] = ..., 
                value_string: Optional[str] = ..., 
                value_time: Optional[time] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentFieldSchema(MutableMapping[str, Any]):
        description: Optional[str]
        example: Optional[str]
        items_schema: Optional[DocumentFieldSchema]
        properties: Optional[Dict[str, ForwardRef('DocumentFieldSchema')]]
        type: Union[str, DocumentFieldType]

        @overload
        def __init__(
                self, 
                *, 
                description: Optional[str] = ..., 
                example: Optional[str] = ..., 
                items_schema: Optional[DocumentFieldSchema] = ..., 
                properties: Optional[Dict[str, DocumentFieldSchema]] = ..., 
                type: Union[str, DocumentFieldType]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentFieldType(str, Enum):
        ADDRESS = "address"
        ARRAY = "array"
        BOOLEAN = "boolean"
        COUNTRY_REGION = "countryRegion"
        CURRENCY = "currency"
        DATE = "date"
        INTEGER = "integer"
        NUMBER = "number"
        OBJECT = "object"
        PHONE_NUMBER = "phoneNumber"
        SELECTION_GROUP = "selectionGroup"
        SELECTION_MARK = "selectionMark"
        SIGNATURE = "signature"
        STRING = "string"
        TIME = "time"


    class azure.ai.documentintelligence.models.DocumentFigure(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        caption: Optional[DocumentCaption]
        elements: Optional[List[str]]
        footnotes: Optional[List[ForwardRef('DocumentFootnote')]]
        id: Optional[str]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                caption: Optional[DocumentCaption] = ..., 
                elements: Optional[List[str]] = ..., 
                footnotes: Optional[List[DocumentFootnote]] = ..., 
                id: Optional[str] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentFontStyle(str, Enum):
        ITALIC = "italic"
        NORMAL = "normal"


    class azure.ai.documentintelligence.models.DocumentFontWeight(str, Enum):
        BOLD = "bold"
        NORMAL = "normal"


    class azure.ai.documentintelligence.models.DocumentFootnote(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        content: str
        elements: Optional[List[str]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                content: str, 
                elements: Optional[List[str]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentFormula(MutableMapping[str, Any]):
        confidence: float
        kind: Union[str, DocumentFormulaKind]
        polygon: Optional[List[float]]
        span: DocumentSpan
        value: str

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                kind: Union[str, DocumentFormulaKind], 
                polygon: Optional[List[float]] = ..., 
                span: DocumentSpan, 
                value: str
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentFormulaKind(str, Enum):
        DISPLAY = "display"
        INLINE = "inline"


    class azure.ai.documentintelligence.models.DocumentIntelligenceError(MutableMapping[str, Any]):
        code: str
        details: Optional[List[ForwardRef('DocumentIntelligenceError')]]
        innererror: Optional[DocumentIntelligenceInnerError]
        message: str
        target: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                code: str, 
                details: Optional[List[DocumentIntelligenceError]] = ..., 
                innererror: Optional[DocumentIntelligenceInnerError] = ..., 
                message: str, 
                target: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentIntelligenceErrorResponse(MutableMapping[str, Any]):
        error: DocumentIntelligenceError

        @overload
        def __init__(
                self, 
                *, 
                error: DocumentIntelligenceError
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentIntelligenceInnerError(MutableMapping[str, Any]):
        code: Optional[str]
        innererror: Optional[DocumentIntelligenceInnerError]
        message: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                code: Optional[str] = ..., 
                innererror: Optional[DocumentIntelligenceInnerError] = ..., 
                message: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentIntelligenceOperationDetails(MutableMapping[str, Any]):
        api_version: Optional[str]
        created_date_time: datetime
        error: Optional[DocumentIntelligenceError]
        kind: str
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: Optional[int]
        resource_location: str
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: Optional[Dict[str, str]]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentIntelligenceOperationStatus(str, Enum):
        CANCELED = "canceled"
        FAILED = "failed"
        NOT_STARTED = "notStarted"
        RUNNING = "running"
        SKIPPED = "skipped"
        SUCCEEDED = "succeeded"


    class azure.ai.documentintelligence.models.DocumentIntelligenceResourceDetails(MutableMapping[str, Any]):
        custom_document_models: CustomDocumentModelsDetails

        @overload
        def __init__(
                self, 
                *, 
                custom_document_models: CustomDocumentModelsDetails
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentIntelligenceWarning(MutableMapping[str, Any]):
        code: str
        message: str
        target: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                code: str, 
                message: str, 
                target: Optional[str] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentKeyValueElement(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        content: str
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                content: str, 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentKeyValuePair(MutableMapping[str, Any]):
        confidence: float
        key: DocumentKeyValueElement
        value: Optional[DocumentKeyValueElement]

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                key: DocumentKeyValueElement, 
                value: Optional[DocumentKeyValueElement] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentLanguage(MutableMapping[str, Any]):
        confidence: float
        locale: str
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                locale: str, 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentLine(MutableMapping[str, Any]):
        content: str
        polygon: Optional[List[float]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                content: str, 
                polygon: Optional[List[float]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentModelBuildOperationDetails(MutableMapping[str, Any]):
        api_version: str
        created_date_time: datetime
        error: DocumentIntelligenceError
        kind: Literal[OperationKind.DOCUMENT_MODEL_BUILD]
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: int
        resource_location: str
        result: Optional[DocumentModelDetails]
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: dict[str, str]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                result: Optional[DocumentModelDetails] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentModelComposeOperationDetails(MutableMapping[str, Any]):
        api_version: str
        created_date_time: datetime
        error: DocumentIntelligenceError
        kind: Literal[OperationKind.DOCUMENT_MODEL_COMPOSE]
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: int
        resource_location: str
        result: Optional[DocumentModelDetails]
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: dict[str, str]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                result: Optional[DocumentModelDetails] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentModelCopyToOperationDetails(MutableMapping[str, Any]):
        api_version: str
        created_date_time: datetime
        error: DocumentIntelligenceError
        kind: Literal[OperationKind.DOCUMENT_MODEL_COPY_TO]
        last_updated_date_time: datetime
        operation_id: str
        percent_completed: int
        resource_location: str
        result: Optional[DocumentModelDetails]
        status: Union[str, DocumentIntelligenceOperationStatus]
        tags: dict[str, str]

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                result: Optional[DocumentModelDetails] = ..., 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...

        @overload
        def __init__(
                self, 
                *, 
                api_version: Optional[str] = ..., 
                created_date_time: datetime, 
                error: Optional[DocumentIntelligenceError] = ..., 
                kind: str, 
                last_updated_date_time: datetime, 
                operation_id: str, 
                percent_completed: Optional[int] = ..., 
                resource_location: str, 
                status: Union[str, DocumentIntelligenceOperationStatus], 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentModelDetails(MutableMapping[str, Any]):
        api_version: Optional[str]
        azure_blob_file_list_source: Optional[AzureBlobFileListContentSource]
        azure_blob_source: Optional[AzureBlobContentSource]
        build_mode: Optional[Union[str, DocumentBuildMode]]
        classifier_id: Optional[str]
        created_date_time: datetime
        description: Optional[str]
        doc_types: Optional[Dict[str, ForwardRef('DocumentTypeDetails')]]
        expiration_date_time: Optional[datetime]
        model_id: str
        modified_date_time: Optional[datetime]
        split: Optional[Union[str, SplitMode]]
        tags: Optional[Dict[str, str]]
        training_hours: Optional[float]
        warnings: Optional[List[ForwardRef('DocumentIntelligenceWarning')]]

        @overload
        def __init__(
                self, 
                *, 
                classifier_id: Optional[str] = ..., 
                description: Optional[str] = ..., 
                model_id: str, 
                split: Optional[Union[str, SplitMode]] = ..., 
                tags: Optional[Dict[str, str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentPage(MutableMapping[str, Any]):
        angle: Optional[float]
        barcodes: Optional[List[ForwardRef('DocumentBarcode')]]
        formulas: Optional[List[ForwardRef('DocumentFormula')]]
        height: Optional[float]
        lines: Optional[List[ForwardRef('DocumentLine')]]
        page_number: int
        selection_marks: Optional[List[ForwardRef('DocumentSelectionMark')]]
        spans: List[DocumentSpan]
        unit: Optional[Union[str, LengthUnit]]
        width: Optional[float]
        words: Optional[List[ForwardRef('DocumentWord')]]

        @overload
        def __init__(
                self, 
                *, 
                angle: Optional[float] = ..., 
                barcodes: Optional[List[DocumentBarcode]] = ..., 
                formulas: Optional[List[DocumentFormula]] = ..., 
                height: Optional[float] = ..., 
                lines: Optional[List[DocumentLine]] = ..., 
                page_number: int, 
                selection_marks: Optional[List[DocumentSelectionMark]] = ..., 
                spans: List[DocumentSpan], 
                unit: Optional[Union[str, LengthUnit]] = ..., 
                width: Optional[float] = ..., 
                words: Optional[List[DocumentWord]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentParagraph(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        content: str
        role: Optional[Union[str, ParagraphRole]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                content: str, 
                role: Optional[Union[str, ParagraphRole]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentSection(MutableMapping[str, Any]):
        elements: Optional[List[str]]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                elements: Optional[List[str]] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentSelectionMark(MutableMapping[str, Any]):
        confidence: float
        polygon: Optional[List[float]]
        span: DocumentSpan
        state: Union[str, DocumentSelectionMarkState]

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                polygon: Optional[List[float]] = ..., 
                span: DocumentSpan, 
                state: Union[str, DocumentSelectionMarkState]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentSelectionMarkState(str, Enum):
        SELECTED = "selected"
        UNSELECTED = "unselected"


    class azure.ai.documentintelligence.models.DocumentSignatureType(str, Enum):
        SIGNED = "signed"
        UNSIGNED = "unsigned"


    class azure.ai.documentintelligence.models.DocumentSpan(MutableMapping[str, Any]):
        length: int
        offset: int

        @overload
        def __init__(
                self, 
                *, 
                length: int, 
                offset: int
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentStyle(MutableMapping[str, Any]):
        background_color: Optional[str]
        color: Optional[str]
        confidence: float
        font_style: Optional[Union[str, DocumentFontStyle]]
        font_weight: Optional[Union[str, DocumentFontWeight]]
        is_handwritten: Optional[bool]
        similar_font_family: Optional[str]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                background_color: Optional[str] = ..., 
                color: Optional[str] = ..., 
                confidence: float, 
                font_style: Optional[Union[str, DocumentFontStyle]] = ..., 
                font_weight: Optional[Union[str, DocumentFontWeight]] = ..., 
                is_handwritten: Optional[bool] = ..., 
                similar_font_family: Optional[str] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentTable(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        caption: Optional[DocumentCaption]
        cells: List[DocumentTableCell]
        column_count: int
        footnotes: Optional[List[ForwardRef('DocumentFootnote')]]
        row_count: int
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                caption: Optional[DocumentCaption] = ..., 
                cells: List[DocumentTableCell], 
                column_count: int, 
                footnotes: Optional[List[DocumentFootnote]] = ..., 
                row_count: int, 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentTableCell(MutableMapping[str, Any]):
        bounding_regions: Optional[List[ForwardRef('BoundingRegion')]]
        column_index: int
        column_span: Optional[int]
        content: str
        elements: Optional[List[str]]
        kind: Optional[Union[str, DocumentTableCellKind]]
        row_index: int
        row_span: Optional[int]
        spans: List[DocumentSpan]

        @overload
        def __init__(
                self, 
                *, 
                bounding_regions: Optional[List[BoundingRegion]] = ..., 
                column_index: int, 
                column_span: Optional[int] = ..., 
                content: str, 
                elements: Optional[List[str]] = ..., 
                kind: Optional[Union[str, DocumentTableCellKind]] = ..., 
                row_index: int, 
                row_span: Optional[int] = ..., 
                spans: List[DocumentSpan]
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentTableCellKind(str, Enum):
        COLUMN_HEADER = "columnHeader"
        CONTENT = "content"
        DESCRIPTION = "description"
        ROW_HEADER = "rowHeader"
        STUB_HEAD = "stubHead"


    class azure.ai.documentintelligence.models.DocumentTypeDetails(MutableMapping[str, Any]):
        build_mode: Optional[Union[str, DocumentBuildMode]]
        confidence_threshold: Optional[float]
        description: Optional[str]
        features: Optional[List[Union[str, ForwardRef('DocumentAnalysisFeature')]]]
        field_confidence: Optional[Dict[str, float]]
        field_schema: Optional[Dict[str, ForwardRef('DocumentFieldSchema')]]
        max_documents_to_analyze: Optional[int]
        model_id: Optional[str]
        query_fields: Optional[List[str]]

        @overload
        def __init__(
                self, 
                *, 
                build_mode: Optional[Union[str, DocumentBuildMode]] = ..., 
                confidence_threshold: Optional[float] = ..., 
                description: Optional[str] = ..., 
                features: Optional[List[Union[str, DocumentAnalysisFeature]]] = ..., 
                field_confidence: Optional[Dict[str, float]] = ..., 
                field_schema: Optional[Dict[str, DocumentFieldSchema]] = ..., 
                max_documents_to_analyze: Optional[int] = ..., 
                model_id: Optional[str] = ..., 
                query_fields: Optional[List[str]] = ...
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.DocumentWord(MutableMapping[str, Any]):
        confidence: float
        content: str
        polygon: Optional[List[float]]
        span: DocumentSpan

        @overload
        def __init__(
                self, 
                *, 
                confidence: float, 
                content: str, 
                polygon: Optional[List[float]] = ..., 
                span: DocumentSpan
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.LengthUnit(str, Enum):
        INCH = "inch"
        PIXEL = "pixel"


    class azure.ai.documentintelligence.models.ModelCopyAuthorization(MutableMapping[str, Any]):
        access_token: str
        expiration_date_time: datetime
        target_model_id: str
        target_model_location: str
        target_resource_id: str
        target_resource_region: str

        @overload
        def __init__(
                self, 
                *, 
                access_token: str, 
                expiration_date_time: datetime, 
                target_model_id: str, 
                target_model_location: str, 
                target_resource_id: str, 
                target_resource_region: str
            ) -> None: ...

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None: ...


    class azure.ai.documentintelligence.models.OperationKind(str, Enum):
        DOCUMENT_CLASSIFIER_BUILD = "documentClassifierBuild"
        DOCUMENT_CLASSIFIER_COPY_TO = "documentClassifierCopyTo"
        DOCUMENT_MODEL_BUILD = "documentModelBuild"
        DOCUMENT_MODEL_COMPOSE = "documentModelCompose"
        DOCUMENT_MODEL_COPY_TO = "documentModelCopyTo"


    class azure.ai.documentintelligence.models.ParagraphRole(str, Enum):
        FOOTNOTE = "footnote"
        FORMULA_BLOCK = "formulaBlock"
        PAGE_FOOTER = "pageFooter"
        PAGE_HEADER = "pageHeader"
        PAGE_NUMBER = "pageNumber"
        SECTION_HEADING = "sectionHeading"
        TITLE = "title"


    class azure.ai.documentintelligence.models.SplitMode(str, Enum):
        AUTO = "auto"
        NONE = "none"
        PER_PAGE = "perPage"


    class azure.ai.documentintelligence.models.StringIndexType(str, Enum):
        TEXT_ELEMENTS = "textElements"
        UNICODE_CODE_POINT = "unicodeCodePoint"
        UTF16_CODE_UNIT = "utf16CodeUnit"


```