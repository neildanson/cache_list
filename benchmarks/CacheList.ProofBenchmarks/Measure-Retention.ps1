param(
    [int]$Repeats = 3,
    [string]$OutputPath = 'BenchmarkDotNet.Artifacts/proof-retention.json'
)
$ErrorActionPreference = 'Stop'
$assembly = Join-Path $PSScriptRoot 'bin/Release/net10.0/CacheList.ProofBenchmarks.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build the proof benchmark project in Release first.' }
$cases = @(
    @{ Payload='Int'; N=4096; Versions=2048; Collections=@('Tree','Array','List','ImmutableList') },
    @{ Payload='Reference'; N=4096; Versions=256; Collections=@('Tree','Array','List','ImmutableList') },
    @{ Payload='Wide64'; N=4096; Versions=256; Collections=@('Tree','Array','List','ImmutableList') },
    @{ Payload='Int'; N=65536; Versions=10000; Collections=@('Tree') },
    @{ Payload='Reference'; N=65536; Versions=10000; Collections=@('Tree') },
    @{ Payload='Wide64'; N=65536; Versions=10000; Collections=@('Tree') }
)
$results = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    foreach ($collection in $case.Collections) {
        for ($repeat=1; $repeat -le $Repeats; $repeat++) {
            $lines = @(& dotnet $assembly --retention $case.Payload $collection $case.N $case.Versions)
            if ($LASTEXITCODE -ne 0) { throw "Retention worker failed: $($case.Payload) $collection" }
            $result = $lines[-1] | ConvertFrom-Json
            $result | Add-Member -NotePropertyName Payload -NotePropertyValue $case.Payload
            $result | Add-Member -NotePropertyName Repeat -NotePropertyValue $repeat
            $results.Add($result)
            Write-Output "$($case.Payload) $collection N=$($case.N) versions=$($case.Versions) repeat=$repeat live=$($result.AdditionalLiveBytes) B"
        }
    }
}
$destination = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
[IO.File]::WriteAllText($destination, ($results | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Output "Saved $($results.Count) isolated-worker measurements to $destination"
