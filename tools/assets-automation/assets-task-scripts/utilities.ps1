function Create-If-Not-Exists {
    param(
        [string]$Path
    )

    if (!(Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force
    }

    return $Path
}