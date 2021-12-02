param (
    $recordingsFolder = "C:\repo\sdk-for-python\sdk\tables\azure-data-tables\tests\recordings",
    $swagger = "C:\repo\azure-rest-api-specs\specification\cosmos-db\data-plane\Microsoft.Tables\preview\2019-02-02\table.json",
    $output = "./oav-output"
)

$payloadOut = "build/validation.txt"

mkdir -p $output

Write-Host "Recordings folder: $recordingsFolder"
Write-Host "Swagger: $swagger"
Write-Host "Output folder: $output"

oavc convert --directory $recordingsFolder --out $output

Write-Host "Recordings Converted, starting validation."

oav validate-traffic $output $swagger -l error > $payloadOut

# clean up validation.txt. Due to how the code is logged, we can't actually just use it as-is.
./regenerate_payload.ps1 -inputPayload $payloadOut
