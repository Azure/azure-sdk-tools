param (
    [Parameter(Mandatory = $true)]
    [string] $SourceBlobPath,
    [Parameter(Mandatory = $true)]
    [string] $SASKey,
    [Parameter(Mandatory = $true)]
    [string] $DestinationDirectory
)

azcopy cp "${SourceBlobPath}?${SASKey}" "${DestinationDirectory}" --recursive=true