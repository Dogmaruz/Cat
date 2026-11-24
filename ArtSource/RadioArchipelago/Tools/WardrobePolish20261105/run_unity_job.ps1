param([Parameter(Mandatory=$true)][string]$Action, [int]$WaitSeconds=45)
$queueFolder = 'C:/Users/Derek/Games_develop/Cat/DancingCatsClone/Library/MusicalChartReview'
if (Test-Path -LiteralPath "$queueFolder/request.json") { throw 'Another Unity job is already queued.' }
$issuedAt = Get-Date
@{action=$Action} | ConvertTo-Json -Compress | Set-Content -LiteralPath "$queueFolder/request.json" -Encoding utf8
$deadline = $issuedAt.AddSeconds($WaitSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 200
    if (Test-Path -LiteralPath "$queueFolder/response.json") {
        $responseFile = Get-Item -LiteralPath "$queueFolder/response.json"
        if ($responseFile.LastWriteTime -ge $issuedAt) {
            try { $response = Get-Content -LiteralPath $responseFile.FullName -Raw | ConvertFrom-Json } catch { continue }
            if ($response.action -eq $Action) {
                $response | ConvertTo-Json -Compress
                if (-not $response.success) { exit 1 }
                exit 0
            }
        }
    }
}
throw "No completed queue response for $Action within $WaitSeconds seconds."
