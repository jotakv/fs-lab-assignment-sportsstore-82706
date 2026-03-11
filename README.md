Vieo Demo:
https://youtu.be/RKDg8MKEZvY

# --- .NET DEVELOPMENT LIFECYCLE ---
# Removes all build artifacts from previous runs
dotnet clean

# Restores project dependencies and NuGet packages
dotnet restore

# Compiles the project and its dependencies
dotnet build

# Executes unit tests and displays results
dotnet test

# --- CONFIGURATION VERIFICATION (TARGET FRAMEWORK) ---
# Checks the .NET version in the main project file
Select-String -Path .\SportsStore\SportsStore.csproj -Pattern "<TargetFramework>"

# Checks the .NET version in the test project file
Select-String -Path .\SportsStore.Tests\SportsStore.Tests.csproj -Pattern "<TargetFramework>"

# --- SYSTEM FILES AND LOGS INSPECTION ---
# Lists the GitHub Actions workflow files
dir .\.github\workflows

# Lists the application log files directory
dir .\SportsStore\Logs

# --- LOG READOUT ---
# Displays the last 40 lines of all log files starting with "log-"
Get-Content .\SportsStore\Logs\log-*.txt -Tail 10
