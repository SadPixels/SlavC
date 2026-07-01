[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $Rid = 'win-x64',
    [string] $Version = ''
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Повелѣніе не удалось съ кодомъ $LASTEXITCODE: $Command"
    }
}

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$hostOutput = Join-Path $artifacts "host-templates/$Rid"
$compilerOutput = Join-Path $artifacts "slavc/$Rid"
$versionArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $versionArguments += "-p:Version=$Version"
}

Invoke-Checked { dotnet restore (Join-Path $root 'SlavLang.slnx') }
Invoke-Checked { dotnet build (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-restore @versionArguments }
Invoke-Checked { dotnet test (Join-Path $root 'SlavLang.slnx') -c $Configuration --no-build }
Invoke-Checked { dotnet publish (Join-Path $root 'src/SlavLang.RuntimeHost/SlavLang.RuntimeHost.csproj') -c $Configuration -r $Rid --self-contained true -o $hostOutput @versionArguments }
Invoke-Checked { dotnet publish (Join-Path $root 'src/SlavLang.Cli/SlavLang.Cli.csproj') -c $Configuration -r $Rid --self-contained true -o $compilerOutput @versionArguments }
