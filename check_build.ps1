# M2 유니티 컴파일 체크 스크립트 (Windows / PowerShell)
#
# 사용법:
#   .\check_build.ps1 -ProjectPath "C:\Users\me\M2_MyeongjinCrazyRacing"
#
# UnityPath는 본인 설치 경로에 맞게 바꿔야 함. Unity Hub 설치 시 보통:
#   C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe

param(
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe",
    [string]$Method = "BuildCheck.CompileCheck"
)

$LogFile = Join-Path $ProjectPath "build.log"

if (-not (Test-Path $UnityPath)) {
    Write-Host "❌ Unity 실행 파일을 찾을 수 없음: $UnityPath" -ForegroundColor Red
    Write-Host "   -UnityPath 파라미터로 실제 설치 경로를 지정해줘 (Unity Hub > 설치 위치 확인)"
    exit 1
}

# Remove any previous log first — otherwise a slow-starting Unity process can leave the
# polling loop below reading stale content from the LAST run (including a leftover
# "error CS" from a since-fixed failure) and report a false failure for a run that
# actually succeeded.
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }

Write-Host "🔧 유니티 컴파일 체크 시작..." -ForegroundColor Cyan
Write-Host "   Project: $ProjectPath"
Write-Host "   Method:  $Method"

& $UnityPath -batchmode -nographics -quit `
    -projectPath $ProjectPath `
    -executeMethod $Method `
    -logFile $LogFile

Write-Host "----------------------------------------"

$existsWaited = 0
while (-not (Test-Path $LogFile) -and $existsWaited -lt 60) {
    Start-Sleep -Seconds 3
    $existsWaited += 3
}

if (-not (Test-Path $LogFile)) {
    Write-Host "❌ 로그 파일이 생성되지 않음. Unity 실행 자체가 실패했을 수 있음." -ForegroundColor Red
    exit 1
}

$log = Get-Content $LogFile -Raw

# The blocking `&` call above can return control a few seconds before Unity has actually
# finished writing the rest of the log (observed repeatedly in practice), so a log that
# has neither a success nor a failure marker yet doesn't necessarily mean the run failed —
# poll briefly before giving up.
$waited = 0
# "Exception(?!s)" — not just "Exception" — so package import log lines that merely
# mention a folder/class named "...Exceptions" (plural, e.g. NGO's own
# Packages/com.unity.netcode.gameobjects/Runtime/Exceptions) don't get mistaken for a
# real thrown exception and end this polling loop early, before Unity has actually
# finished writing the real success/failure marker later in the log (observed in
# practice: this caused a false failure report on a run that had actually succeeded).
while ($log -notmatch "M2_.*_OK|error CS|Exception(?!s)|M2_.*_FAIL" -and $waited -lt 60) {
    Start-Sleep -Seconds 3
    $waited += 3
    $log = Get-Content $LogFile -Raw
}

# Generic "M2_..._OK" instead of an enumerated list of marker names — so any new
# BuildCheck entry point (e.g. BuildNetworkVehiclePrefab) is recognized automatically
# without having to edit this script again every time BuildCheck.cs gains a new method.
if ($log -match "M2_.*_OK") {
    Write-Host "✅ 컴파일 성공!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ 컴파일 에러 또는 실행 실패:" -ForegroundColor Red
    Write-Host ""
    Select-String -Path $LogFile -Pattern "error CS|Exception(?!s)|M2_.*_FAIL" | ForEach-Object { $_.Line }
    Write-Host ""
    Write-Host "전체 로그: $LogFile"
    exit 1
}