param (
    $recordingsFolder,
    $swagger,
    $output
)

mkdir -p $output

Write-Host "Recordings folder: $recordingsFolder"
Write-Host "Swagger: $swagger"
Write-Host "Output folder: $output"

oavc convert --directory $recordingsFolder --out $output

Write-Host "Recordings Converted, starting validation."

oav validate-traffic $output $swagger -l error &> build/validation.txt

