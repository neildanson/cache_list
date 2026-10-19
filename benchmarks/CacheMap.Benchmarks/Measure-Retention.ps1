param(
    [int]$N = 4096,
    [int]$Steps = 1023,
    [int]$Repeats = 3,
    [string]$Output = "docs/cache-map-retention.json"
)
$ErrorActionPreference = 'Stop'
if ($N -lt 1 -or $Steps -lt 0 -or $Repeats -lt 1) { throw 'Invalid measurement dimensions' }
$project = Join-Path $PSScriptRoot 'CacheMap.Benchmarks.fsproj'
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$assembly = Join-Path $PSScriptRoot 'bin/Release/net10.0/CacheMap.Benchmarks.dll'
$results = @()
# Fresh process per measurement; never run workers concurrently.
foreach ($payload in @('int','string')) {
    foreach ($implementation in @('packed','map')) {
        for ($repeat = 1; $repeat -le $Repeats; $repeat++) {
            $raw = & dotnet $assembly --retention $payload $implementation $N $Steps
            if ($LASTEXITCODE -ne 0) { throw "Retention worker failed: $payload / $implementation" }
            $row = $raw | ConvertFrom-Json
            $row | Add-Member -NotePropertyName repeat -NotePropertyValue $repeat
            $results += $row
            Write-Host "$payload / $implementation / repeat $repeat : $($row.liveBytes) live bytes"
        }
    }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Output -Encoding utf8
