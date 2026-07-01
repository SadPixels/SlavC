[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $Rid = 'win-x64',
    [string] $Version = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$hostOutput = Join-Path $artifacts "host-templates/$Rid"
$compilerOutput = Join-Path $artifacts "slavc/$Rid"
$versionArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $versionArguments += "-p:Version=$Version"
}

dotnet restore (Join-Path $root 'SlavLang.slnx')
dotnet build (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-restore @versionArguments
dotnet test (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-build
dotnet publish (Join-Path $root 'src/SlavLang.RuntimeHost/SlavLang.RuntimeHost.csproj') `
    -c $Configuration -r $Rid --self-contained true -o $hostOutput @versionArguments
dotnet publish (Join-Path $root 'src/SlavLang.Cli/SlavLang.Cli.csproj') `
    -c $Configuration -r $Rid --self-contained true -o $compilerOutput @versionArguments
