param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectOutputPath,
    
    [Parameter(Mandatory=$true)]
    [string]$TestDataPath
)

Write-Host "Copying TestData to output directory..."
Write-Host "Source: $TestDataPath"
Write-Host "Destination: $ProjectOutputPath"

# Ensure the output directory exists
if (!(Test-Path $ProjectOutputPath)) {
    New-Item -ItemType Directory -Path $ProjectOutputPath -Force | Out-Null
    Write-Host "Created output directory: $ProjectOutputPath"
}

# Remove existing TestData folder if it exists
$testDataDestPath = Join-Path $ProjectOutputPath "TestData"
if (Test-Path $testDataDestPath) {
    Write-Host "Removing existing TestData folder..."
    Remove-Item -Path $testDataDestPath -Recurse -Force
}

# Copy TestData folder to output directory
Write-Host "Copying TestData folder..."
Copy-Item -Path $TestDataPath -Destination $ProjectOutputPath -Recurse -Force

Write-Host "TestData copied successfully to: $testDataDestPath" 