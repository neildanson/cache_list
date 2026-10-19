$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '../../src/CacheList/CacheList.fs'
$source = [IO.File]::ReadAllText($sourcePath)
$checks = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Checks.template.fs'))
$outputPath = Join-Path $PSScriptRoot 'obj/capacity-sources'
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)

foreach ($width in 16, 32, 64) {
    $variant = $source
    $replacements = @{
        '(?m)^namespace CacheCollections\r?$' = "namespace CacheCollections.Capacity$width"
        '(?m)^    let width = \d+\r?$' = "    let width = $width"
        '(?m)^    let minimum = \d+\r?$' = "    let minimum = $($width / 2)"
    }
    foreach ($pattern in $replacements.Keys) {
        if ([regex]::Matches($variant, $pattern).Count -ne 1) {
            throw "Expected one match for: $pattern"
        }
        $variant = [regex]::Replace($variant, $pattern, $replacements[$pattern])
    }
    [IO.File]::WriteAllText((Join-Path $outputPath "Capacity$width.fs"), $variant, $encoding)
    $validation = $checks.Replace('__WIDTH__', "$width")
    [IO.File]::WriteAllText((Join-Path $outputPath "Checks$width.fs"), $validation, $encoding)
}
Write-Output 'Generated capacity variants 16/8, 32/16, and 64/32 from the current library source.'
