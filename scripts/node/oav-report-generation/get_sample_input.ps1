param (
    $recordingsFolder = "C:\repo\sdk-for-python\sdk\tables\azure-data-tables\tests\recordings",
    $swagger = "C:\repo\azure-rest-api-specs\specification\cosmos-db\data-plane\Microsoft.Tables\preview\2019-02-02\table.json",
    $output = "./oav-output"
)

mkdir -p $output

Write-Host "Recordings folder: $recordingsFolder"
Write-Host "Swagger: $swagger"
Write-Host "Output folder: $output"

oavc convert --directory $recordingsFolder --out $output

Write-Host "Recordings Converted, starting validation."

oav validate-traffic $output $swagger -l error > build/validation.txt

# do a quick replace to clean up the output into an actual array

