[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$hostOutput = Join-Path $artifacts "host-templates/$Rid"
$compilerOutput = Join-Path $artifacts "slavc/$Rid"

dotnet restore (Join-Path $root 'SlavLang.slnx')
dotnet build (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-restore
dotnet test (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-build
dotnet publish (Join-Path $root 'src/SlavLang.RuntimeHost/SlavLang.RuntimeHost.csproj') `
    -c $Configuration -r $Rid --self-contained true -o $hostOutput
dotnet publish (Join-Path $root 'src/SlavLang.Cli/SlavLang.Cli.csproj') `
    -c $Configuration -r $Rid --self-contained true -o $compilerOutput
