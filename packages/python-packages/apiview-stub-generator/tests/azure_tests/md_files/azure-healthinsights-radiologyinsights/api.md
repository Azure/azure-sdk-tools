```python
# Package is parsed using apiview-stub-generator(version:0.3.26), Python version: 3.10.12


namespace azure.healthinsights.radiologyinsights

    class azure.healthinsights.radiologyinsights.RadiologyInsightsClient: implements ContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, TokenCredential], 
                *, 
                api_version: str = ..., 
                polling_interval: Optional[int] = ..., 
                **kwargs: Any
            ) -> None

        @overload
        def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: RadiologyInsightsJob, 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> LROPoller[RadiologyInsightsInferenceResult]

        @overload
        def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: JSON, 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> LROPoller[RadiologyInsightsInferenceResult]

        @overload
        def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> LROPoller[RadiologyInsightsInferenceResult]

        def close(self) -> None

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> HttpResponse


namespace azure.healthinsights.radiologyinsights.aio

    class azure.healthinsights.radiologyinsights.aio.RadiologyInsightsClient: implements AsyncContextManager 

        def __init__(
                self, 
                endpoint: str, 
                credential: Union[AzureKeyCredential, AsyncTokenCredential], 
                *, 
                api_version: str = ..., 
                polling_interval: Optional[int] = ..., 
                **kwargs: Any
            ) -> None

        @overload
        async def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: RadiologyInsightsJob, 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[RadiologyInsightsInferenceResult]

        @overload
        async def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: JSON, 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[RadiologyInsightsInferenceResult]

        @overload
        async def begin_infer_radiology_insights(
                self, 
                id: str, 
                resource: IO[bytes], 
                *, 
                content_type: str = "application/json", 
                expand: Optional[List[str]] = ..., 
                **kwargs: Any
            ) -> AsyncLROPoller[RadiologyInsightsInferenceResult]

        async def close(self) -> None

        def send_request(
                self, 
                request: HttpRequest, 
                *, 
                stream: bool = False, 
                **kwargs: Any
            ) -> Awaitable[AsyncHttpResponse]


namespace azure.healthinsights.radiologyinsights.models

    class azure.healthinsights.radiologyinsights.models.AgeMismatchInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.AGE_MISMATCH]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Annotation(MutableMapping[str, Any]):
        ivar author_string: Optional[str]
        ivar extension: list[Extension]
        ivar id: str
        ivar text: str
        ivar time: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                author_string: Optional[str] = ..., 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                text: str, 
                time: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.AssessmentValueRange(MutableMapping[str, Any]):
        ivar maximum: str
        ivar minimum: str

        @overload
        def __init__(
                self, 
                *, 
                maximum: str, 
                minimum: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ClinicalDocumentType(str, Enum):
        CONSULTATION = "consultation"
        DISCHARGE_SUMMARY = "dischargeSummary"
        HISTORY_AND_PHYSICAL = "historyAndPhysical"
        LABORATORY = "laboratory"
        PATHOLOGY_REPORT = "pathologyReport"
        PROCEDURE = "procedure"
        PROGRESS = "progress"
        RADIOLOGY_REPORT = "radiologyReport"


    class azure.healthinsights.radiologyinsights.models.CodeableConcept(MutableMapping[str, Any]):
        ivar coding: Optional[List[ForwardRef('Coding')]]
        ivar text: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                coding: Optional[List[Coding]] = ..., 
                text: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Coding(MutableMapping[str, Any]):
        ivar code: Optional[str]
        ivar display: Optional[str]
        ivar extension: list[Extension]
        ivar id: str
        ivar system: Optional[str]
        ivar version: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                code: Optional[str] = ..., 
                display: Optional[str] = ..., 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                system: Optional[str] = ..., 
                version: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.CompleteOrderDiscrepancyInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.COMPLETE_ORDER_DISCREPANCY]
        ivar missing_body_part_measurements: Optional[List[ForwardRef('CodeableConcept')]]
        ivar missing_body_parts: Optional[List[ForwardRef('CodeableConcept')]]
        ivar order_type: CodeableConcept

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                missing_body_part_measurements: Optional[List[CodeableConcept]] = ..., 
                missing_body_parts: Optional[List[CodeableConcept]] = ..., 
                order_type: CodeableConcept
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ContactPointSystem(str, Enum):
        EMAIL = "email"
        FAX = "fax"
        OTHER = "other"
        PAGER = "pager"
        PHONE = "phone"
        SMS = "sms"
        URL = "url"


    class azure.healthinsights.radiologyinsights.models.ContactPointUse(str, Enum):
        HOME = "home"
        MOBILE = "mobile"
        OLD = "old"
        TEMP = "temp"
        WORK = "work"


    class azure.healthinsights.radiologyinsights.models.CriticalResult(MutableMapping[str, Any]):
        ivar description: str
        ivar finding: Optional[Observation]

        @overload
        def __init__(
                self, 
                *, 
                description: str, 
                finding: Optional[Observation] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.CriticalResultInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.CRITICAL_RESULT]
        ivar result: CriticalResult

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                result: CriticalResult
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.DocumentAdministrativeMetadata(MutableMapping[str, Any]):
        ivar encounter_id: Optional[str]
        ivar ordered_procedures: Optional[List[ForwardRef('OrderedProcedure')]]

        @overload
        def __init__(
                self, 
                *, 
                encounter_id: Optional[str] = ..., 
                ordered_procedures: Optional[List[OrderedProcedure]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.DocumentAuthor(MutableMapping[str, Any]):
        ivar full_name: Optional[str]
        ivar id: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                full_name: Optional[str] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.DocumentContent(MutableMapping[str, Any]):
        ivar source_type: Union[str, DocumentContentSourceType]
        ivar value: str

        @overload
        def __init__(
                self, 
                *, 
                source_type: Union[str, DocumentContentSourceType], 
                value: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.DocumentContentSourceType(str, Enum):
        INLINE = "inline"
        REFERENCE = "reference"


    class azure.healthinsights.radiologyinsights.models.DocumentType(str, Enum):
        DICOM = "dicom"
        FHIR_BUNDLE = "fhirBundle"
        GENOMIC_SEQUENCING = "genomicSequencing"
        NOTE = "note"


    class azure.healthinsights.radiologyinsights.models.DomainResource(MutableMapping[str, Any]):
        ivar contained: Optional[List[ForwardRef('Resource')]]
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar id: str
        ivar implicit_rules: str
        ivar language: str
        ivar meta: Meta
        ivar modifier_extension: Optional[List[ForwardRef('Extension')]]
        ivar resource_type: str
        ivar text: Optional[Narrative]

        @overload
        def __init__(
                self, 
                *, 
                contained: Optional[List[Resource]] = ..., 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                implicit_rules: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                modifier_extension: Optional[List[Extension]] = ..., 
                resource_type: str, 
                text: Optional[Narrative] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                id: Optional[str] = ..., 
                implicit_rules: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                resource_type: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Element(MutableMapping[str, Any]):
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar id: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.EncounterClass(str, Enum):
        AMBULATORY = "ambulatory"
        EMERGENCY = "emergency"
        HEALTH_HOME = "healthHome"
        IN_PATIENT = "inpatient"
        OBSERVATION = "observation"
        VIRTUAL = "virtual"


    class azure.healthinsights.radiologyinsights.models.Extension(MutableMapping[str, Any]):
        ivar url: str
        ivar value_boolean: Optional[bool]
        ivar value_codeable_concept: Optional[CodeableConcept]
        ivar value_date_time: Optional[str]
        ivar value_integer: Optional[int]
        ivar value_period: Optional[Period]
        ivar value_quantity: Optional[Quantity]
        ivar value_range: Optional[Range]
        ivar value_ratio: Optional[Ratio]
        ivar value_reference: Optional[Reference]
        ivar value_sampled_data: Optional[SampledData]
        ivar value_string: Optional[str]
        ivar value_time: Optional[time]

        @overload
        def __init__(
                self, 
                *, 
                url: str, 
                value_boolean: Optional[bool] = ..., 
                value_codeable_concept: Optional[CodeableConcept] = ..., 
                value_date_time: Optional[str] = ..., 
                value_integer: Optional[int] = ..., 
                value_period: Optional[Period] = ..., 
                value_quantity: Optional[Quantity] = ..., 
                value_range: Optional[Range] = ..., 
                value_ratio: Optional[Ratio] = ..., 
                value_reference: Optional[Reference] = ..., 
                value_sampled_data: Optional[SampledData] = ..., 
                value_string: Optional[str] = ..., 
                value_time: Optional[time] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.FindingInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar finding: Observation
        ivar kind: Literal[RadiologyInsightsInferenceType.FINDING]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                finding: Observation
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.FindingOptions(MutableMapping[str, Any]):
        ivar provide_focused_sentence_evidence: Optional[bool]

        @overload
        def __init__(
                self, 
                *, 
                provide_focused_sentence_evidence: Optional[bool] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.FollowupCommunicationInference(MutableMapping[str, Any]):
        ivar communicated_at: Optional[List[datetime]]
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.FOLLOWUP_COMMUNICATION]
        ivar recipient: Optional[List[Union[str, ForwardRef('MedicalProfessionalType')]]]
        ivar was_acknowledged: bool

        @overload
        def __init__(
                self, 
                *, 
                communicated_at: Optional[List[datetime]] = ..., 
                extension: Optional[List[Extension]] = ..., 
                recipient: Optional[List[Union[str, MedicalProfessionalType]]] = ..., 
                was_acknowledged: bool
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.FollowupRecommendationInference(MutableMapping[str, Any]):
        ivar effective_at: Optional[str]
        ivar effective_period: Optional[Period]
        ivar extension: list[Extension]
        ivar findings: Optional[List[ForwardRef('RecommendationFinding')]]
        ivar is_conditional: bool
        ivar is_guideline: bool
        ivar is_hedging: bool
        ivar is_option: bool
        ivar kind: Literal[RadiologyInsightsInferenceType.FOLLOWUP_RECOMMENDATION]
        ivar recommended_procedure: ProcedureRecommendation

        @overload
        def __init__(
                self, 
                *, 
                effective_at: Optional[str] = ..., 
                effective_period: Optional[Period] = ..., 
                extension: Optional[List[Extension]] = ..., 
                findings: Optional[List[RecommendationFinding]] = ..., 
                is_conditional: bool, 
                is_guideline: bool, 
                is_hedging: bool, 
                is_option: bool, 
                recommended_procedure: ProcedureRecommendation
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.FollowupRecommendationOptions(MutableMapping[str, Any]):
        ivar include_recommendations_in_references: Optional[bool]
        ivar include_recommendations_with_no_specified_modality: Optional[bool]
        ivar provide_focused_sentence_evidence: Optional[bool]

        @overload
        def __init__(
                self, 
                *, 
                include_recommendations_in_references: Optional[bool] = ..., 
                include_recommendations_with_no_specified_modality: Optional[bool] = ..., 
                provide_focused_sentence_evidence: Optional[bool] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.GenericProcedureRecommendation(MutableMapping[str, Any]):
        ivar code: CodeableConcept
        ivar description: Optional[str]
        ivar extension: list[Extension]
        ivar kind: Literal["genericProcedureRecommendation"]

        @overload
        def __init__(
                self, 
                *, 
                code: CodeableConcept, 
                description: Optional[str] = ..., 
                extension: Optional[List[Extension]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.GuidanceInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar finding: FindingInference
        ivar identifier: CodeableConcept
        ivar kind: Literal[RadiologyInsightsInferenceType.GUIDANCE]
        ivar missing_guidance_information: Optional[List[str]]
        ivar present_guidance_information: Optional[List[ForwardRef('PresentGuidanceInformation')]]
        ivar ranking: Union[str, GuidanceRankingType]
        ivar recommendation_proposals: Optional[List[ForwardRef('FollowupRecommendationInference')]]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                finding: FindingInference, 
                identifier: CodeableConcept, 
                missing_guidance_information: Optional[List[str]] = ..., 
                present_guidance_information: Optional[List[PresentGuidanceInformation]] = ..., 
                ranking: Union[str, GuidanceRankingType], 
                recommendation_proposals: Optional[List[FollowupRecommendationInference]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.GuidanceOptions(MutableMapping[str, Any]):
        ivar show_guidance_in_history: bool

        @overload
        def __init__(
                self, 
                *, 
                show_guidance_in_history: bool
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.GuidanceRankingType(str, Enum):
        HIGH = "high"
        LOW = "low"


    class azure.healthinsights.radiologyinsights.models.HealthInsightsErrorResponse(MutableMapping[str, Any]):
        ivar error: ODataV4Format

        @overload
        def __init__(
                self, 
                *, 
                error: ODataV4Format
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Identifier(MutableMapping[str, Any]):
        ivar assigner: Optional[Reference]
        ivar period: Optional[Period]
        ivar system: Optional[str]
        ivar type: Optional[CodeableConcept]
        ivar use: Optional[str]
        ivar value: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                assigner: Optional[Reference] = ..., 
                period: Optional[Period] = ..., 
                system: Optional[str] = ..., 
                type: Optional[CodeableConcept] = ..., 
                use: Optional[str] = ..., 
                value: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ImagingProcedure(MutableMapping[str, Any]):
        ivar anatomy: CodeableConcept
        ivar contrast: Optional[RadiologyCodeWithTypes]
        ivar laterality: Optional[CodeableConcept]
        ivar modality: CodeableConcept
        ivar view: Optional[RadiologyCodeWithTypes]

        @overload
        def __init__(
                self, 
                *, 
                anatomy: CodeableConcept, 
                contrast: Optional[RadiologyCodeWithTypes] = ..., 
                laterality: Optional[CodeableConcept] = ..., 
                modality: CodeableConcept, 
                view: Optional[RadiologyCodeWithTypes] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ImagingProcedureRecommendation(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar imaging_procedures: List[ImagingProcedure]
        ivar kind: Literal["imagingProcedureRecommendation"]
        ivar procedure_codes: Optional[List[ForwardRef('CodeableConcept')]]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                imaging_procedures: List[ImagingProcedure], 
                procedure_codes: Optional[List[CodeableConcept]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.JobStatus(str, Enum):
        CANCELED = "canceled"
        FAILED = "failed"
        NOT_STARTED = "notStarted"
        RUNNING = "running"
        SUCCEEDED = "succeeded"


    class azure.healthinsights.radiologyinsights.models.LateralityDiscrepancyInference(MutableMapping[str, Any]):
        ivar discrepancy_type: Union[str, LateralityDiscrepancyType]
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.LATERALITY_DISCREPANCY]
        ivar laterality_indication: Optional[CodeableConcept]

        @overload
        def __init__(
                self, 
                *, 
                discrepancy_type: Union[str, LateralityDiscrepancyType], 
                extension: Optional[List[Extension]] = ..., 
                laterality_indication: Optional[CodeableConcept] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.LateralityDiscrepancyType(str, Enum):
        ORDER_LATERALITY_MISMATCH = "orderLateralityMismatch"
        TEXT_LATERALITY_CONTRADICTION = "textLateralityContradiction"
        TEXT_LATERALITY_MISSING = "textLateralityMissing"


    class azure.healthinsights.radiologyinsights.models.LimitedOrderDiscrepancyInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.LIMITED_ORDER_DISCREPANCY]
        ivar order_type: CodeableConcept
        ivar present_body_part_measurements: Optional[List[ForwardRef('CodeableConcept')]]
        ivar present_body_parts: Optional[List[ForwardRef('CodeableConcept')]]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                order_type: CodeableConcept, 
                present_body_part_measurements: Optional[List[CodeableConcept]] = ..., 
                present_body_parts: Optional[List[CodeableConcept]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.MedicalProfessionalType(str, Enum):
        DOCTOR = "doctor"
        MIDWIFE = "midwife"
        NURSE = "nurse"
        PHYSICIAN_ASSISTANT = "physicianAssistant"
        UNKNOWN = "unknown"


    class azure.healthinsights.radiologyinsights.models.Meta(MutableMapping[str, Any]):
        ivar last_updated: Optional[str]
        ivar profile: Optional[List[str]]
        ivar security: Optional[List[ForwardRef('Coding')]]
        ivar source: Optional[str]
        ivar tag: Optional[List[ForwardRef('Coding')]]
        ivar version_id: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                last_updated: Optional[str] = ..., 
                profile: Optional[List[str]] = ..., 
                security: Optional[List[Coding]] = ..., 
                source: Optional[str] = ..., 
                tag: Optional[List[Coding]] = ..., 
                version_id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Narrative(MutableMapping[str, Any]):
        ivar div: str
        ivar extension: list[Extension]
        ivar id: str
        ivar status: str

        @overload
        def __init__(
                self, 
                *, 
                div: str, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                status: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Observation(MutableMapping[str, Any]):
        ivar body_site: Optional[CodeableConcept]
        ivar category: Optional[List[ForwardRef('CodeableConcept')]]
        ivar code: CodeableConcept
        ivar component: Optional[List[ForwardRef('ObservationComponent')]]
        ivar contained: list[Resource]
        ivar data_absent_reason: Optional[CodeableConcept]
        ivar derived_from: Optional[List[ForwardRef('Reference')]]
        ivar effective_date_time: Optional[str]
        ivar effective_instant: Optional[str]
        ivar effective_period: Optional[Period]
        ivar encounter: Optional[Reference]
        ivar extension: list[Extension]
        ivar has_member: Optional[List[ForwardRef('Reference')]]
        ivar id: str
        ivar identifier: Optional[List[ForwardRef('Identifier')]]
        ivar implicit_rules: str
        ivar interpretation: Optional[List[ForwardRef('CodeableConcept')]]
        ivar issued: Optional[str]
        ivar language: str
        ivar meta: Meta
        ivar method: Optional[CodeableConcept]
        ivar modifier_extension: list[Extension]
        ivar note: Optional[List[ForwardRef('Annotation')]]
        ivar reference_range: Optional[List[ForwardRef('ObservationReferenceRange')]]
        ivar resource_type: Literal["Observation"]
        ivar status: Union[str, ObservationStatusCodeType]
        ivar subject: Optional[Reference]
        ivar text: Narrative
        ivar value_boolean: Optional[bool]
        ivar value_codeable_concept: Optional[CodeableConcept]
        ivar value_date_time: Optional[str]
        ivar value_integer: Optional[int]
        ivar value_period: Optional[Period]
        ivar value_quantity: Optional[Quantity]
        ivar value_range: Optional[Range]
        ivar value_ratio: Optional[Ratio]
        ivar value_sampled_data: Optional[SampledData]
        ivar value_string: Optional[str]
        ivar value_time: Optional[time]

        @overload
        def __init__(
                self, 
                *, 
                body_site: Optional[CodeableConcept] = ..., 
                category: Optional[List[CodeableConcept]] = ..., 
                code: CodeableConcept, 
                component: Optional[List[ObservationComponent]] = ..., 
                contained: Optional[List[Resource]] = ..., 
                data_absent_reason: Optional[CodeableConcept] = ..., 
                derived_from: Optional[List[Reference]] = ..., 
                effective_date_time: Optional[str] = ..., 
                effective_instant: Optional[str] = ..., 
                effective_period: Optional[Period] = ..., 
                encounter: Optional[Reference] = ..., 
                extension: Optional[List[Extension]] = ..., 
                has_member: Optional[List[Reference]] = ..., 
                id: Optional[str] = ..., 
                identifier: Optional[List[Identifier]] = ..., 
                implicit_rules: Optional[str] = ..., 
                interpretation: Optional[List[CodeableConcept]] = ..., 
                issued: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                method: Optional[CodeableConcept] = ..., 
                modifier_extension: Optional[List[Extension]] = ..., 
                note: Optional[List[Annotation]] = ..., 
                reference_range: Optional[List[ObservationReferenceRange]] = ..., 
                status: Union[str, ObservationStatusCodeType], 
                subject: Optional[Reference] = ..., 
                text: Optional[Narrative] = ..., 
                value_boolean: Optional[bool] = ..., 
                value_codeable_concept: Optional[CodeableConcept] = ..., 
                value_date_time: Optional[str] = ..., 
                value_integer: Optional[int] = ..., 
                value_period: Optional[Period] = ..., 
                value_quantity: Optional[Quantity] = ..., 
                value_range: Optional[Range] = ..., 
                value_ratio: Optional[Ratio] = ..., 
                value_sampled_data: Optional[SampledData] = ..., 
                value_string: Optional[str] = ..., 
                value_time: Optional[time] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                contained: Optional[List[Resource]] = ..., 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                implicit_rules: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                modifier_extension: Optional[List[Extension]] = ..., 
                resource_type: str, 
                text: Optional[Narrative] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                id: Optional[str] = ..., 
                implicit_rules: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                resource_type: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ObservationComponent(MutableMapping[str, Any]):
        ivar code: CodeableConcept
        ivar data_absent_reason: Optional[CodeableConcept]
        ivar extension: list[Extension]
        ivar id: str
        ivar interpretation: Optional[List[ForwardRef('CodeableConcept')]]
        ivar reference_range: Optional[List[ForwardRef('ObservationReferenceRange')]]
        ivar value_boolean: Optional[bool]
        ivar value_codeable_concept: Optional[CodeableConcept]
        ivar value_date_time: Optional[str]
        ivar value_integer: Optional[int]
        ivar value_period: Optional[Period]
        ivar value_quantity: Optional[Quantity]
        ivar value_range: Optional[Range]
        ivar value_ratio: Optional[Ratio]
        ivar value_reference: Optional[Reference]
        ivar value_sampled_data: Optional[SampledData]
        ivar value_string: Optional[str]
        ivar value_time: Optional[time]

        @overload
        def __init__(
                self, 
                *, 
                code: CodeableConcept, 
                data_absent_reason: Optional[CodeableConcept] = ..., 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ..., 
                interpretation: Optional[List[CodeableConcept]] = ..., 
                reference_range: Optional[List[ObservationReferenceRange]] = ..., 
                value_boolean: Optional[bool] = ..., 
                value_codeable_concept: Optional[CodeableConcept] = ..., 
                value_date_time: Optional[str] = ..., 
                value_integer: Optional[int] = ..., 
                value_period: Optional[Period] = ..., 
                value_quantity: Optional[Quantity] = ..., 
                value_range: Optional[Range] = ..., 
                value_ratio: Optional[Ratio] = ..., 
                value_reference: Optional[Reference] = ..., 
                value_sampled_data: Optional[SampledData] = ..., 
                value_string: Optional[str] = ..., 
                value_time: Optional[time] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ObservationReferenceRange(MutableMapping[str, Any]):
        ivar age: Optional[Range]
        ivar applies_to: Optional[List[ForwardRef('CodeableConcept')]]
        ivar high: Optional[Quantity]
        ivar low: Optional[Quantity]
        ivar text: Optional[str]
        ivar type: Optional[CodeableConcept]

        @overload
        def __init__(
                self, 
                *, 
                age: Optional[Range] = ..., 
                applies_to: Optional[List[CodeableConcept]] = ..., 
                high: Optional[Quantity] = ..., 
                low: Optional[Quantity] = ..., 
                text: Optional[str] = ..., 
                type: Optional[CodeableConcept] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ObservationStatusCodeType(str, Enum):
        AMENDED = "amended"
        CANCELLED = "cancelled"
        CORRECTED = "corrected"
        ENTERED_IN_ERROR = "entered-in-error"
        FINAL = "final"
        PRELIMINARY = "preliminary"
        REGISTERED = "registered"
        UNKNOWN = "unknown"


    class azure.healthinsights.radiologyinsights.models.OrderedProcedure(MutableMapping[str, Any]):
        ivar code: Optional[CodeableConcept]
        ivar description: Optional[str]
        ivar extension: Optional[List[ForwardRef('Extension')]]

        @overload
        def __init__(
                self, 
                *, 
                code: Optional[CodeableConcept] = ..., 
                description: Optional[str] = ..., 
                extension: Optional[List[Extension]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PatientDetails(MutableMapping[str, Any]):
        ivar birth_date: Optional[date]
        ivar clinical_info: Optional[List[ForwardRef('Resource')]]
        ivar sex: Optional[Union[str, PatientSex]]

        @overload
        def __init__(
                self, 
                *, 
                birth_date: Optional[date] = ..., 
                clinical_info: Optional[List[Resource]] = ..., 
                sex: Optional[Union[str, PatientSex]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PatientDocument(MutableMapping[str, Any]):
        ivar administrative_metadata: Optional[DocumentAdministrativeMetadata]
        ivar authors: Optional[List[ForwardRef('DocumentAuthor')]]
        ivar clinical_type: Optional[Union[str, ClinicalDocumentType]]
        ivar content: DocumentContent
        ivar created_at: Optional[datetime]
        ivar id: str
        ivar language: Optional[str]
        ivar specialty_type: Optional[Union[str, SpecialtyType]]
        ivar type: Union[str, DocumentType]

        @overload
        def __init__(
                self, 
                *, 
                administrative_metadata: Optional[DocumentAdministrativeMetadata] = ..., 
                authors: Optional[List[DocumentAuthor]] = ..., 
                clinical_type: Optional[Union[str, ClinicalDocumentType]] = ..., 
                content: DocumentContent, 
                created_at: Optional[datetime] = ..., 
                id: str, 
                language: Optional[str] = ..., 
                specialty_type: Optional[Union[str, SpecialtyType]] = ..., 
                type: Union[str, DocumentType]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PatientEncounter(MutableMapping[str, Any]):
        ivar class_property: Optional[Union[str, EncounterClass]]
        ivar id: str
        ivar period: Optional[TimePeriod]

        @overload
        def __init__(
                self, 
                *, 
                class_property: Optional[Union[str, EncounterClass]] = ..., 
                id: str, 
                period: Optional[TimePeriod] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PatientRecord(MutableMapping[str, Any]):
        ivar details: Optional[PatientDetails]
        ivar encounters: Optional[List[ForwardRef('PatientEncounter')]]
        ivar id: str
        ivar patient_documents: Optional[List[ForwardRef('PatientDocument')]]

        @overload
        def __init__(
                self, 
                *, 
                details: Optional[PatientDetails] = ..., 
                encounters: Optional[List[PatientEncounter]] = ..., 
                id: str, 
                patient_documents: Optional[List[PatientDocument]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PatientSex(str, Enum):
        FEMALE = "female"
        MALE = "male"
        UNSPECIFIED = "unspecified"


    class azure.healthinsights.radiologyinsights.models.Period(MutableMapping[str, Any]):
        ivar end: Optional[str]
        ivar start: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                end: Optional[str] = ..., 
                start: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.PresentGuidanceInformation(MutableMapping[str, Any]):
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar maximum_diameter_as_in_text: Optional[Quantity]
        ivar present_guidance_item: str
        ivar present_guidance_values: Optional[List[str]]
        ivar sizes: Optional[List[ForwardRef('Observation')]]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                maximum_diameter_as_in_text: Optional[Quantity] = ..., 
                present_guidance_item: str, 
                present_guidance_values: Optional[List[str]] = ..., 
                sizes: Optional[List[Observation]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ProcedureRecommendation(MutableMapping[str, Any]):
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar kind: str

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.QualityMeasureComplianceType(str, Enum):
        DENOMINATOR_EXCEPTION = "denominatorException"
        NOT_ELIGIBLE = "notEligible"
        PERFORMANCE_MET = "performanceMet"
        PERFORMANCE_NOT_MET = "performanceNotMet"


    class azure.healthinsights.radiologyinsights.models.QualityMeasureInference(MutableMapping[str, Any]):
        ivar compliance_type: Union[str, QualityMeasureComplianceType]
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.QUALITY_MEASURE]
        ivar quality_criteria: Optional[List[str]]
        ivar quality_measure_denominator: str

        @overload
        def __init__(
                self, 
                *, 
                compliance_type: Union[str, QualityMeasureComplianceType], 
                extension: Optional[List[Extension]] = ..., 
                quality_criteria: Optional[List[str]] = ..., 
                quality_measure_denominator: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.QualityMeasureOptions(MutableMapping[str, Any]):
        ivar measure_types: List[Union[str, ForwardRef('QualityMeasureType')]]

        @overload
        def __init__(
                self, 
                *, 
                measure_types: List[Union[str, QualityMeasureType]]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.QualityMeasureType(str, Enum):
        ACRAD36 = "acrad36"
        ACRAD37 = "acrad37"
        ACRAD38 = "acrad38"
        ACRAD39 = "acrad39"
        ACRAD40 = "acrad40"
        ACRAD41 = "acrad41"
        ACRAD42 = "acrad42"
        MEDNAX55 = "mednax55"
        MIPS145 = "mips145"
        MIPS147 = "mips147"
        MIPS195 = "mips195"
        MIPS360 = "mips360"
        MIPS364 = "mips364"
        MIPS405 = "mips405"
        MIPS406 = "mips406"
        MIPS436 = "mips436"
        MIPS76 = "mips76"
        MSN13 = "msn13"
        MSN15 = "msn15"
        QMM17 = "qmm17"
        QMM18 = "qmm18"
        QMM19 = "qmm19"
        QMM26 = "qmm26"


    class azure.healthinsights.radiologyinsights.models.Quantity(MutableMapping[str, Any]):
        ivar code: Optional[str]
        ivar comparator: Optional[str]
        ivar system: Optional[str]
        ivar unit: Optional[str]
        ivar value: Optional[float]

        @overload
        def __init__(
                self, 
                *, 
                code: Optional[str] = ..., 
                comparator: Optional[str] = ..., 
                system: Optional[str] = ..., 
                unit: Optional[str] = ..., 
                value: Optional[float] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyCodeWithTypes(MutableMapping[str, Any]):
        ivar code: CodeableConcept
        ivar types: List[CodeableConcept]

        @overload
        def __init__(
                self, 
                *, 
                code: CodeableConcept, 
                types: List[CodeableConcept]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsData(MutableMapping[str, Any]):
        ivar configuration: Optional[RadiologyInsightsModelConfiguration]
        ivar patients: List[PatientRecord]

        @overload
        def __init__(
                self, 
                *, 
                configuration: Optional[RadiologyInsightsModelConfiguration] = ..., 
                patients: List[PatientRecord]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsInference(MutableMapping[str, Any]):
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar kind: str

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsInferenceOptions(MutableMapping[str, Any]):
        ivar finding_options: Optional[FindingOptions]
        ivar followup_recommendation_options: Optional[FollowupRecommendationOptions]
        ivar guidance_options: Optional[GuidanceOptions]
        ivar quality_measure_options: Optional[QualityMeasureOptions]

        @overload
        def __init__(
                self, 
                *, 
                finding_options: Optional[FindingOptions] = ..., 
                followup_recommendation_options: Optional[FollowupRecommendationOptions] = ..., 
                guidance_options: Optional[GuidanceOptions] = ..., 
                quality_measure_options: Optional[QualityMeasureOptions] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsInferenceResult(MutableMapping[str, Any]):
        ivar model_version: str
        ivar patient_results: List[RadiologyInsightsPatientResult]

        @overload
        def __init__(
                self, 
                *, 
                model_version: str, 
                patient_results: List[RadiologyInsightsPatientResult]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsInferenceType(str, Enum):
        AGE_MISMATCH = "ageMismatch"
        COMPLETE_ORDER_DISCREPANCY = "completeOrderDiscrepancy"
        CRITICAL_RESULT = "criticalResult"
        FINDING = "finding"
        FOLLOWUP_COMMUNICATION = "followupCommunication"
        FOLLOWUP_RECOMMENDATION = "followupRecommendation"
        GUIDANCE = "guidance"
        LATERALITY_DISCREPANCY = "lateralityDiscrepancy"
        LIMITED_ORDER_DISCREPANCY = "limitedOrderDiscrepancy"
        QUALITY_MEASURE = "qualityMeasure"
        RADIOLOGY_PROCEDURE = "radiologyProcedure"
        SCORING_AND_ASSESSMENT = "scoringAndAssessment"
        SEX_MISMATCH = "sexMismatch"


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsJob(MutableMapping[str, Any]):
        ivar created_at: Optional[datetime]
        ivar error: Optional[ODataV4Format]
        ivar expires_at: Optional[datetime]
        ivar id: str
        ivar job_data: Optional[RadiologyInsightsData]
        ivar result: Optional[RadiologyInsightsInferenceResult]
        ivar status: Union[str, JobStatus]
        ivar updated_at: Optional[datetime]

        @overload
        def __init__(
                self, 
                *, 
                job_data: Optional[RadiologyInsightsData] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsModelConfiguration(MutableMapping[str, Any]):
        ivar include_evidence: Optional[bool]
        ivar inference_options: Optional[RadiologyInsightsInferenceOptions]
        ivar inference_types: Optional[List[Union[str, ForwardRef('RadiologyInsightsInferenceType')]]]
        ivar locale: Optional[str]
        ivar verbose: Optional[bool]

        @overload
        def __init__(
                self, 
                *, 
                include_evidence: Optional[bool] = ..., 
                inference_options: Optional[RadiologyInsightsInferenceOptions] = ..., 
                inference_types: Optional[List[Union[str, RadiologyInsightsInferenceType]]] = ..., 
                locale: Optional[str] = ..., 
                verbose: Optional[bool] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyInsightsPatientResult(MutableMapping[str, Any]):
        ivar inferences: List[RadiologyInsightsInference]
        ivar patient_id: str

        @overload
        def __init__(
                self, 
                *, 
                inferences: List[RadiologyInsightsInference], 
                patient_id: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RadiologyProcedureInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar imaging_procedures: List[ImagingProcedure]
        ivar kind: Literal[RadiologyInsightsInferenceType.RADIOLOGY_PROCEDURE]
        ivar ordered_procedure: OrderedProcedure
        ivar procedure_codes: Optional[List[ForwardRef('CodeableConcept')]]

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                imaging_procedures: List[ImagingProcedure], 
                ordered_procedure: OrderedProcedure, 
                procedure_codes: Optional[List[CodeableConcept]] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Range(MutableMapping[str, Any]):
        ivar high: Optional[Quantity]
        ivar low: Optional[Quantity]

        @overload
        def __init__(
                self, 
                *, 
                high: Optional[Quantity] = ..., 
                low: Optional[Quantity] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.Ratio(MutableMapping[str, Any]):
        ivar denominator: Optional[Quantity]
        ivar numerator: Optional[Quantity]

        @overload
        def __init__(
                self, 
                *, 
                denominator: Optional[Quantity] = ..., 
                numerator: Optional[Quantity] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RecommendationFinding(MutableMapping[str, Any]):
        ivar critical_finding: Optional[CriticalResult]
        ivar extension: Optional[List[ForwardRef('Extension')]]
        ivar finding: Optional[Observation]
        ivar recommendation_finding_status: Union[str, RecommendationFindingStatusType]

        @overload
        def __init__(
                self, 
                *, 
                critical_finding: Optional[CriticalResult] = ..., 
                extension: Optional[List[Extension]] = ..., 
                finding: Optional[Observation] = ..., 
                recommendation_finding_status: Union[str, RecommendationFindingStatusType]
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.RecommendationFindingStatusType(str, Enum):
        CONDITIONAL = "conditional"
        DIFFERENTIAL = "differential"
        PRESENT = "present"
        RULE_OUT = "ruleOut"


    class azure.healthinsights.radiologyinsights.models.Reference(MutableMapping[str, Any]):
        ivar display: Optional[str]
        ivar identifier: Optional[Identifier]
        ivar reference: Optional[str]
        ivar type: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                display: Optional[str] = ..., 
                identifier: Optional[Identifier] = ..., 
                reference: Optional[str] = ..., 
                type: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ResearchStudyStatusCodeType(str, Enum):
        ACTIVE = "active"
        ADMINISTRATIVELY_COMPLETED = "administratively-completed"
        APPROVED = "approved"
        CLOSED_TO_ACCRUAL = "closed-to-accrual"
        CLOSED_TO_ACCRUAL_AND_INTERVENTION = "closed-to-accrual-and-intervention"
        COMPLETED = "completed"
        DISAPPROVED = "disapproved"
        IN_REVIEW = "in-review"
        TEMPORARILY_CLOSED_TO_ACCRUAL = "temporarily-closed-to-accrual"
        TEMPORARILY_CLOSED_TO_ACCRUAL_AND_INTERVENTION = "temporarily-closed-to-accrual-and-intervention"
        WITHDRAWN = "withdrawn"


    class azure.healthinsights.radiologyinsights.models.Resource(MutableMapping[str, Any]):
        ivar id: Optional[str]
        ivar implicit_rules: Optional[str]
        ivar language: Optional[str]
        ivar meta: Optional[Meta]
        ivar resource_type: str

        @overload
        def __init__(
                self, 
                *, 
                id: Optional[str] = ..., 
                implicit_rules: Optional[str] = ..., 
                language: Optional[str] = ..., 
                meta: Optional[Meta] = ..., 
                resource_type: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.SampledData(MutableMapping[str, Any]):
        ivar data: Optional[str]
        ivar dimensions: int
        ivar factor: Optional[float]
        ivar lower_limit: Optional[float]
        ivar origin: Quantity
        ivar period: float
        ivar upper_limit: Optional[float]

        @overload
        def __init__(
                self, 
                *, 
                data: Optional[str] = ..., 
                dimensions: int, 
                factor: Optional[float] = ..., 
                lower_limit: Optional[float] = ..., 
                origin: Quantity, 
                period: float, 
                upper_limit: Optional[float] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                id: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.ScoringAndAssessmentCategoryType(str, Enum):
        AGATSTON_SCORE = "AGATSTON SCORE"
        ALBERTA_STROKE_PROGRAM_EARLY_CT_SCORE = "ALBERTA STROKE PROGRAM EARLY CT SCORE"
        ASCVD_RISK = "ASCVD RISK"
        BIRADS = "BIRADS"
        CAD_RADS = "CAD-RADS"
        CALCIUM_MASS_SCORE = "CALCIUM MASS SCORE"
        CALCIUM_SCORE_UNSPECIFIED = "CALCIUM SCORE (UNSPECIFIED)"
        CALCIUM_VOLUME_SCORE = "CALCIUM VOLUME SCORE"
        CEUS_LI_RADS = "CEUS LI-RADS"
        C_RADS_COLONIC_FINDINGS = "C-RADS COLONIC FINDINGS"
        C_RADS_EXTRACOLONIC_FINDINGS = "C-RADS EXTRACOLONIC FINDINGS"
        FRAX_SCORE = "FRAX SCORE"
        HNPCC_MUTATION_RISK = "HNPCC MUTATION RISK"
        KELLGREN_LAWRENCE_GRADING_SCALE = "KELLGREN-LAWRENCE GRADING SCALE"
        LIFETIME_BREAST_CANCER_RISK = "LIFETIME BREAST CANCER RISK"
        LI_RADS = "LI-RADS"
        LUNG_RADS = "LUNG-RADS"
        MODIFIED_GAIL_MODEL_RISK = "MODIFIED GAIL MODEL RISK"
        NI_RADS = "NI-RADS"
        O_RADS = "O-RADS"
        O_RADS_MRI = "O-RADS MRI"
        PI_RADS = "PI-RADS"
        RISK_OF_MALIGNANCY_INDEX = "RISK OF MALIGNANCY INDEX"
        TEN_YEAR_CHD_RISK = "10 YEAR CHD RISK"
        TEN_YEAR_CHD_RISK_ARTERIAL_AGE = "10 YEAR CHD RISK (ARTERIAL AGE)"
        TEN_YEAR_CHD_RISK_OBSERVED_AGE = "10 YEAR CHD RISK (OBSERVED AGE)"
        TI_RADS = "TI-RADS"
        TONNIS_CLASSIFICATION = "TONNIS CLASSIFICATION"
        TREATMENT_RESPONSE_LI_RADS = "TREATMENT RESPONSE LI-RADS"
        TYRER_CUSICK_MODEL_RISK = "TYRER CUSICK MODEL RISK"
        T_SCORE = "T-SCORE"
        US_LI_RADS = "US LI-RADS"
        US_LI_RADS_VISUALIZATION_SCORE = "US LI-RADS VISUALIZATION SCORE"
        Z_SCORE = "Z-SCORE"


    class azure.healthinsights.radiologyinsights.models.ScoringAndAssessmentInference(MutableMapping[str, Any]):
        ivar category: Union[str, ScoringAndAssessmentCategoryType]
        ivar category_description: str
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.SCORING_AND_ASSESSMENT]
        ivar range_value: Optional[AssessmentValueRange]
        ivar single_value: Optional[str]

        @overload
        def __init__(
                self, 
                *, 
                category: Union[str, ScoringAndAssessmentCategoryType], 
                category_description: str, 
                extension: Optional[List[Extension]] = ..., 
                range_value: Optional[AssessmentValueRange] = ..., 
                single_value: Optional[str] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.SexMismatchInference(MutableMapping[str, Any]):
        ivar extension: list[Extension]
        ivar kind: Literal[RadiologyInsightsInferenceType.SEX_MISMATCH]
        ivar sex_indication: CodeableConcept

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                sex_indication: CodeableConcept
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None

        @overload
        def __init__(
                self, 
                *, 
                extension: Optional[List[Extension]] = ..., 
                kind: str
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


    class azure.healthinsights.radiologyinsights.models.SpecialtyType(str, Enum):
        PATHOLOGY = "pathology"
        RADIOLOGY = "radiology"


    class azure.healthinsights.radiologyinsights.models.TimePeriod(MutableMapping[str, Any]):
        ivar end: Optional[datetime]
        ivar start: Optional[datetime]

        @overload
        def __init__(
                self, 
                *, 
                end: Optional[datetime] = ..., 
                start: Optional[datetime] = ...
            ) -> None

        @overload
        def __init__(self, mapping: Mapping[str, Any]) -> None


```