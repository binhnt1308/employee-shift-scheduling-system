$ErrorActionPreference = "Stop"

$backendRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $backendRoot "ShiftScheduling.sln"
$webProject = Join-Path $backendRoot "ShiftScheduling.Web"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay .NET SDK. Hay cai .NET 8 SDK va mo lai terminal."
}

$oldArchitectureFolders = @(
    (Join-Path $webProject "Controllers"),
    (Join-Path $webProject "Services"),
    (Join-Path $webProject "Repositories"),
    (Join-Path $webProject "Models")
)

foreach ($folder in $oldArchitectureFolders) {
    if (Test-Path $folder) {
        throw "Con thu muc kien truc cu: $folder"
    }
}

$controllerDbContextReferences = Get-ChildItem $webProject -Recurse -Filter "*Controller.cs" |
    Select-String -Pattern "ApplicationDbContext|DbContext"
if ($controllerDbContextReferences) {
    throw "Controller dang truy cap DbContext truc tiep."
}

dotnet restore $solutionPath
dotnet format $solutionPath --verify-no-changes
dotnet build $solutionPath --no-restore -warnaserror
dotnet test $solutionPath --no-build --verbosity normal

Write-Host "PROJECT VERIFY: PASS"
