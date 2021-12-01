param (
    $inputArray,
    $outDirectory = "build"
)

tsc generate_report.ts --out $outDirectory --input $inputArray