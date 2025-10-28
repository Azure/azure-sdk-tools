#!/bin/bash

# Script to process all Rust API reports and generate APIView outputs
# Uses ts-node for development mode processing

echo "🚀 Processing all Rust API reports..."
echo "📁 Input folder: ./inputs/"
echo "📁 Output folder: ./outputs/"

# Create outputs directory if it doesn't exist
mkdir -p outputs

# Track processing statistics
total_packages=0
successful_packages=0
failed_packages=0
failed_package_names=()

# Process each JSON file in the inputs directory
for input_file in ./inputs/*.rust.json; do
    # Extract package name without path and extension
    package_name=$(basename "$input_file" .rust.json)
    output_file="./outputs/${package_name}.json"
    
    echo ""
    echo "📦 Processing: $package_name"
    echo "   Input:  $input_file"
    echo "   Output: $output_file"
    
    total_packages=$((total_packages + 1))
    
    # Run the parser with ts-node
    if ts-node src/main.ts "$input_file" "$output_file"; then
        echo "   ✅ SUCCESS: Generated $output_file"
        successful_packages=$((successful_packages + 1))
    else
        echo "   ❌ FAILED: Error processing $package_name"
        failed_packages=$((failed_packages + 1))
        failed_package_names+=("$package_name")
    fi
done

echo ""
echo "📊 PROCESSING SUMMARY"
echo "====================="
echo "Total packages:      $total_packages"
echo "Successful:          $successful_packages"
echo "Failed:              $failed_packages"

if [ $failed_packages -gt 0 ]; then
    echo ""
    echo "❌ Failed packages:"
    for package in "${failed_package_names[@]}"; do
        echo "   - $package"
    done
    echo ""
    echo "⚠️  Some packages failed to process. Check the error messages above."
    exit 1
else
    echo ""
    echo "🎉 All packages processed successfully!"
    echo "✅ All outputs are available in the ./outputs/ directory"
fi
