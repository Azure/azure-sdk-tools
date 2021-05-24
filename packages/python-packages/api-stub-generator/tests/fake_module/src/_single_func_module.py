# encoding: utf-8
# A sample "module" for testing


def validate_token(self, options=None, signers=None):
    # type: (TokenValidationOptions, list[AttestationSigner]) -> bool
    """Validate the attestation token based on the options specified in the
        :class:`TokenValidationOptions`.

    :param azure.security.attestation.TokenValidationOptions options: Options to be used when validating
        the token.
    :keyword list[azure.security.attestation.AttestationSigner] signers: Potential signers for the token.
        If the signers parameter is specified, validate_token will only
        consider the signers as potential signatories for the token, otherwise
        it will consider attributes in the header of the token.
    :return bool: Returns True if the token successfully validated, False
        otherwise.

    :raises: azure.security.attestation.AttestationTokenValidationException
    """
    pass

def detect_language(  # type: ignore
    self,
    documents,  # type: Union[List[str], List[DetectLanguageInput], List[Dict[str, str]]]
    **kwargs  # type: Any
):
    # type: (...) -> List[Union[DetectLanguageResult, DocumentError]]
    """Detect language for a batch of documents.
    Returns the detected language and a numeric score between zero and
    one. Scores close to one indicate 100% certainty that the identified
    language is true. See https://aka.ms/talangs for the list of enabled languages.
    See https://docs.microsoft.com/azure/cognitive-services/text-analytics/overview#data-limits
    for document length limits, maximum batch size, and supported text encoding.
    :param documents: The set of documents to process as part of this batch.
        If you wish to specify the ID and country_hint on a per-item basis you must
        use as input a list[:class:`~azure.ai.textanalytics.DetectLanguageInput`] or a list of
        dict representations of :class:`~azure.ai.textanalytics.DetectLanguageInput`, like
        `{"id": "1", "country_hint": "us", "text": "hello world"}`.
    :type documents:
        list[str] or list[~azure.ai.textanalytics.DetectLanguageInput] or
        list[dict[str, str]]
    :keyword str country_hint: Country of origin hint for the entire batch. Accepts two
        letter country codes specified by ISO 3166-1 alpha-2. Per-document
        country hints will take precedence over whole batch hints. Defaults to
        "US". If you don't want to use a country hint, pass the string "none".
    :keyword str model_version: Version of the model used on the service side for scoring,
        e.g. "latest", "2019-10-01". If a model version
        is not specified, the API will default to the latest, non-preview version.
        See here for more info: https://aka.ms/text-analytics-model-versioning
    :keyword bool show_stats: If set to true, response will contain document
        level statistics in the `statistics` field of the document-level response.
    :keyword bool disable_service_logs: If set to true, you opt-out of having your text input
        logged on the service side for troubleshooting. By default, Text Analytics logs your
        input text for 48 hours, solely to allow for troubleshooting issues in providing you with
        the Text Analytics natural language processing functions. Setting this parameter to true,
        disables input logging and may limit our ability to remediate issues that occur. Please see
        Cognitive Services Compliance and Privacy notes at https://aka.ms/cs-compliance for
        additional details, and Microsoft Responsible AI principles at
        https://www.microsoft.com/ai/responsible-ai.
    :return: The combined list of :class:`~azure.ai.textanalytics.DetectLanguageResult` and
        :class:`~azure.ai.textanalytics.DocumentError` in the order the original documents were
        passed in.
    :rtype: list[~azure.ai.textanalytics.DetectLanguageResult,
        ~azure.ai.textanalytics.DocumentError]
    :raises ~azure.core.exceptions.HttpResponseError or TypeError or ValueError:
    .. admonition:: Example:
        .. literalinclude:: ../samples/sample_detect_language.py
            :start-after: [START detect_language]
            :end-before: [END detect_language]
            :language: python
            :dedent: 8
            :caption: Detecting language in a batch of documents.
    """
    pass