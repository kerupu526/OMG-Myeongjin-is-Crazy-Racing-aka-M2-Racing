# M2 유니티 PlayMode 테스트 실행 스크립트 (Windows / PowerShell)
#
# 사용법:
#   .\check_playtest.ps1 -ProjectPath "C:\Users\me\M2_MyeongjinCrazyRacing"
#
# 디스플레이가 없는 헤드리스 환경에서는 -nographics 없이 실행하면 그래픽 디바이스
# 생성 단계에서 무한 대기함(창을 띄울 화면이 없어서). 그래서 기본값은 -nographics 사용.
# 실제 GPU/디스플레이가 있는 머신에서 스크린샷 테스트의 실제 렌더링 결과를 보려면
# -Graphics 스위치를 붙여 재정의할 것 (단, 디스플레이 없는 환경에서는 멈출 수 있음):
#   .\check_playtest.ps1 -ProjectPath "..." -Graphics
# (cmd.exe에서 실행할 때도 안전하도록 bool 파라미터 대신 스위치로 뺌 — cmd는 $false를
#  해석하지 못하고 리터럴 문자열로 넘겨서 예전 -NoGraphics:$false 방식은 cmd.exe에서 깨짐)
# UnityPath는 check_build.ps1과 동일하게 맞출 것.

param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe",
    [string]$TestPlatform = "PlayMode",
    [string]$ResultsFile = "playtest_results.xml",
    [switch]$Graphics
)

$NoGraphics = -not $Graphics

$LogFile = Join-Path $ProjectPath "playtest.log"
$ResultsPath = Join-Path $ProjectPath $ResultsFile

if (-not (Test-Path $UnityPath)) {
    Write-Host "❌ Unity 실행 파일을 찾을 수 없음: $UnityPath" -ForegroundColor Red
    Write-Host "   -UnityPath 파라미터로 실제 설치 경로를 지정해줘 (Unity Hub > 설치 위치 확인)"
    exit 1
}

if (Test-Path $ResultsPath) { Remove-Item $ResultsPath -Force }

Write-Host "🎮 유니티 PlayMode 테스트 실행 시작..." -ForegroundColor Cyan
Write-Host "   Project: $ProjectPath"
Write-Host "   Platform: $TestPlatform"
Write-Host "   NoGraphics: $NoGraphics"

$graphicsArgs = @()
if ($NoGraphics) { $graphicsArgs += "-nographics" }

& $UnityPath -batchmode @graphicsArgs `
    -projectPath $ProjectPath `
    -runTests `
    -testPlatform $TestPlatform `
    -testResults $ResultsPath `
    -logFile $LogFile

Write-Host "----------------------------------------"

if (-not (Test-Path $ResultsPath)) {
    Write-Host "❌ 결과 파일이 생성되지 않음. Unity 실행 자체가 실패했을 수 있음." -ForegroundColor Red
    Write-Host "로그: $LogFile"
    if (Test-Path $LogFile) {
        Select-String -Path $LogFile -Pattern "error CS|Exception" | Select-Object -Last 20 | ForEach-Object { $_.Line }
    }
    exit 1
}

[xml]$results = Get-Content $ResultsPath
$run = $results.'test-run'

$total = $run.total
$passed = $run.passed
$failed = $run.failed
$skipped = $run.skipped

Write-Host "총 $total 개 / 통과 $passed / 실패 $failed / 건너뜀 $skipped"

if ([int]$failed -gt 0) {
    Write-Host ""
    Write-Host "❌ 실패한 테스트:" -ForegroundColor Red
    $results.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        Write-Host "  - $($_.fullname)" -ForegroundColor Red
        $message = $_.SelectSingleNode("failure/message")
        if ($message) { Write-Host "    $($message.InnerText)" }
    }
    Write-Host ""
    Write-Host "결과 파일: $ResultsPath"
    Write-Host "로그: $LogFile"
    exit 1
} else {
    Write-Host "✅ 전체 테스트 통과!" -ForegroundColor Green
    exit 0
}
