$RELEASE_TITLE_REGEX = "(?<releaseNoteTitle>^\#+.*(?<version>\b\d+\.\d+\.\d+([^0-9\s][^\s:]+)?))"

function Version-Matches($line)
{
    return ($line -match $RELEASE_TITLE_REGEX)
}

Export-ModuleMember -Function 'Version-Matches'