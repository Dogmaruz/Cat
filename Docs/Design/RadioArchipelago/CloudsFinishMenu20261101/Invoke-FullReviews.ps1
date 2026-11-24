$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location -LiteralPath $repoRoot
$invoke = Join-Path $repoRoot 'DancingCatsClone/Library/MusicalChartReview/Invoke-Review.ps1'
$cases = @(
    @{ Name='Emerald'; Action='island-wide-playtest'; Folder='DancingCatsClone/Library/IslandPilotReview' },
    @{ Name='Coral'; Action='coral-playtest'; Folder='DancingCatsClone/Library/RadioArchipelago/CoralOasis/PlayReview' },
    @{ Name='Moon'; Action='moon-playtest'; Folder='DancingCatsClone/Library/RadioArchipelago/MoonResonance/PlayReview' }
)
foreach ($case in $cases) {
    $started = [DateTime]::UtcNow
    & $invoke -Actions $case.Action
    $reportPath = Join-Path $repoRoot ($case.Folder + '/play-review.json')
    $completed = $false
    while (([DateTime]::UtcNow - $started).TotalSeconds -lt 300) {
        Start-Sleep -Milliseconds 500
        if (!(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -lt $started) { continue }
        try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } catch { continue }
        if ($report.state -eq 'failed') { throw ($case.Name + ': ' + ($report.errors -join [Environment]::NewLine)) }
        if ($report.state -ne 'passed' -or !$report.inputRestored -or !$report.profileRestored) { continue }
        $target = Join-Path $PSScriptRoot ('Review/' + $case.Name)
        [IO.Directory]::CreateDirectory($target) | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath (Split-Path -Parent $reportPath) -File) {
            if ($file.Name.Contains($report.runId) -or $file.Name -eq 'play-review.json') {
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
        Write-Output ($case.Name + ' ACTUAL FULL PASS ' + $report.runId + ' contacts=' + $report.scoredScoreContacts + '/' + $report.requiredScoreContacts + ' rise=' + $report.maxDepartureRise + ' forward=' + $report.maxDepartureForward)
        $completed = $true
        break
    }
    if (!$completed) { throw ($case.Name + ' full native review timed out') }
    Start-Sleep -Milliseconds 750
    & $invoke -Actions 'inspect'
}
& $invoke -Actions 'moon-open'
