# encoding: utf-8
# A sample "module" for testing

def validate_token(self, options=None, signers=None):
    # type: (TokenValidationOptions, list[AttestationSigner]) -> bool
    """ Validate the attestation token based on the options specified in the
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