$acct = "apiview"
$assemblies_container = "assemblies"
$comments_container = "comments"
$sas_token = Read-Host -Prompt "SAS token for $($acct)"

if (-Not (Test-Path .\azcopy.exe)) {
    Invoke-WebRequest https://azcopyvnext.azureedge.net/release20190517/azcopy_windows_amd64_10.1.2.zip -OutFile dl.zip
    Expand-Archive dl.zip
    cp .\dl\*\azcopy.exe .
}

./azcopy make "https://$($acct).blob.core.windows.net/$($assemblies_container)$($sas_token)"
./azcopy make "https://$($acct).blob.core.windows.net/$($comments_container)$($sas_token)"

./azcopy copy "./$($assemblies_container)/*" "https://$($acct).blob.core.windows.net/$($assemblies_container)$($sas_token)" --recursive
./azcopy copy "./$($comments_container)/*" "https://$($acct).blob.core.windows.net/$($comments_container)$($sas_token)" --recursive