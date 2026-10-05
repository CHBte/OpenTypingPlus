<#
단어 필터 도구 (<261003_1>(4)) — build.bat 이 앱을 빌드하기 전에, 그리고 dotnet build 도 OpenTyping.csproj 의 ApplyWordFilter 단계로
(2026-10-06) 자동으로 실행한다.

  ① 필터링 파일(OpenTyping\Resources\필터링_단어.txt)을 읽어 모든 항목을 검사한다(<261003_1>(4.6)).
     올바른 UTF-8이 아니거나, 깨진 글자(U+FFFD)·한글/영문 이외의 글자가 든 항목이 있으면 어느 항목이
     문제인지 알려 주고 실패(종료 코드 1)로 끝난다 — build.bat 도 그 자리에서 빌드를 멈춘다.
  ② 기존 단어 필터링 단어(두 글자 이상 항목)를 배포 원본 단어 목록(OpenTyping\wordslist\words.json)과
     내장 예비본(OpenTyping\Resources\words_fallback.json)에서 지운다. 정확히 같은 단어만 지우고, 영문은
     대소문자를 가리지 않는다. 한 음절 필터링 단어(한 글자 항목)는 단어 목록에 적혀 있지 않으므로(실행 때
     계산됨) 여기서는 검사만 하고, 실행 중인 Open Typing Plus 가 적용한다(<261003_1>(5)).
  ③ json 은 문자열 치환이 아니라 해석해서 고친다 — 단계·수준의 "words" 배열 안에서 그 항목만 빼고,
     파일의 나머지 모양(들여쓰기·줄바꿈·순서)은 그대로 둔다. 지울 것이 없으면 파일을 다시 쓰지 않는다.
  ④ 무엇을 지웠는지 단계·수준별로 출력하고, 지운 결과 단어가 하나도 남지 않은 단계·수준이 있으면 경고한다.

필터링 파일의 해석 규칙은 프로그램과 같은 코드(OpenTyping\FilterWords.cs)를 그대로 컴파일해 쓴다.

사용 예
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\apply-word-filter.ps1
    (검사용) ... -FilterFile <txt> -WordsJson <json> -FallbackJson <json>
#>
[CmdletBinding()]
param(
    [string]$FilterFile   = "",
    [string]$WordsJson    = "",
    [string]$FallbackJson = ""
)

$ErrorActionPreference = "Stop"
$otp = Join-Path (Split-Path -Parent $PSScriptRoot) "OpenTyping"
$filterFileGiven = $FilterFile -ne ""
if ($FilterFile   -eq "") { $FilterFile   = Join-Path $otp "Resources\필터링_단어.txt" }
if ($WordsJson    -eq "") { $WordsJson    = Join-Path $otp "wordslist\words.json" }
if ($FallbackJson -eq "") { $FallbackJson = Join-Path $otp "Resources\words_fallback.json" }

function Fail($msg) {
    Write-Host ("[단어 필터 도구] 실패: " + $msg)
    exit 1
}

try {
    # 필터링 파일 해석 규칙(프로그램과 같은 코드 OpenTyping\FilterWords.cs)과 단어 목록 json 해석·수정기
    # (tools\WordListEditor.cs)를 함께 컴파일한다(PowerShell 5.1 의 Add-Type 은 옛 C# 5 컴파일러).
    Add-Type -Path @((Join-Path $otp "FilterWords.cs"), (Join-Path $PSScriptRoot "WordListEditor.cs"))
} catch {
    Fail ("도구를 준비하지 못했습니다: " + $_.Exception.Message)
}

# ---------- ① 필터링 파일 검사 ----------
if (-not (Test-Path -LiteralPath $FilterFile)) {
    # 필터링 파일은 공개 저장소에 올리지 않는다(.gitignore). 저장소를 새로 받은 경우처럼 기본 위치에 없으면 함께 올린 예시 파일
    # (Resources\필터링_단어.example.txt, 한 음절만)로 대신해 빌드가 멈추지 않게 한다. -FilterFile 로 준 파일이 없으면 그대로 실패한다.
    $example = Join-Path $otp "Resources\필터링_단어.example.txt"
    if (-not $filterFileGiven -and (Test-Path -LiteralPath $example)) {
        Write-Host ("[단어 필터 도구] 참고: 필터링 파일이 없어 예시 파일로 대신합니다: " + $example)
        $FilterFile = $example
    } else { Fail "필터링 파일이 없습니다: $FilterFile" }
}
$bytes = [System.IO.File]::ReadAllBytes($FilterFile)
try {
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)   # 올바르지 않은 바이트에서 예외
    $filterText = $strictUtf8.GetString($bytes)
} catch {
    Fail ("필터링 파일이 올바른 UTF-8이 아닙니다(메모장이면 '다른 이름으로 저장'에서 인코딩을 UTF-8로): " + $FilterFile)
}

$entries = [OpenTyping.FilterWords]::Parse($filterText)
$problems = @($entries | Where-Object { $_.Problem -ne $null })
if ($problems.Count -gt 0) {
    foreach ($p in $problems) { Write-Host ("[단어 필터 도구] {0}번째 줄 '{1}': {2}" -f $p.Line, $p.Text, $p.Problem) }
    Fail ("필터링 파일에 문제 항목이 {0}개 있습니다: {1}" -f $problems.Count, $FilterFile)
}

$syllables = @($entries | Where-Object { $_.IsSyllable })
$wordEntries = @($entries | Where-Object { -not $_.IsSyllable })
foreach ($e in $syllables) {
    if ($e.Text -cmatch '^[A-Za-z]$') { Write-Host ("[단어 필터 도구] 참고: {0}번째 줄 '{1}'은 영문 한 글자라 한 음절 목록(한글)에 영향이 없습니다." -f $e.Line, $e.Text) }
}
Write-Host ("[단어 필터 도구] 필터링 파일 확인: 한 음절 {0}개(실행 중 적용), 기존 단어 {1}개" -f $syllables.Count, $wordEntries.Count)

# ---------- ② 기존 단어 필터링 단어를 단어 목록에서 지우기 ----------
$keys = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($e in $wordEntries) { [void]$keys.Add([OpenTyping.FilterWords]::WordKey($e.Text)) }

function Apply-ToFile($path) {
    if (-not (Test-Path -LiteralPath $path)) { Fail "단어 목록 파일이 없습니다: $path" }
    $raw = [System.IO.File]::ReadAllBytes($path)
    $hasBom = $raw.Length -ge 3 -and $raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF
    try { $text = (New-Object System.Text.UTF8Encoding($false, $true)).GetString($raw) }
    catch { Fail "단어 목록 파일이 올바른 UTF-8이 아닙니다: $path" }
    if ($hasBom) { $text = $text.Substring(1) }

    $report = New-Object 'System.Collections.Generic.List[string]'
    $warnings = New-Object 'System.Collections.Generic.List[string]'
    $removed = 0
    try { $newText = [WordListEditor]::RemoveWords($text, $keys, $report, $warnings, [ref]$removed) }
    catch { Fail ("단어 목록을 고치지 못했습니다(" + $path + "): " + $_.Exception.Message) }

    $name = Split-Path -Leaf $path
    if ($removed -eq 0) {
        Write-Host ("[단어 필터 도구] {0}: 지울 단어 없음(파일 그대로)" -f $name)
        return
    }
    $left = [WordListEditor]::Remaining($newText, $keys)
    if ($left.Count -gt 0) { Fail ("지운 뒤에도 필터링 단어가 남았습니다(" + $name + "): " + ($left -join ", ")) }

    try {
        [System.IO.File]::WriteAllText($path, $newText, (New-Object System.Text.UTF8Encoding($hasBom)))
    } catch { Fail ("단어 목록 파일을 쓰지 못했습니다(" + $path + "): " + $_.Exception.Message) }

    Write-Host ("[단어 필터 도구] {0}: {1}개 지움" -f $name, $removed)
    foreach ($line in $report) { Write-Host ("    - " + $line) }
    foreach ($w in $warnings) { Write-Host ("[단어 필터 도구] 경고: {0} — {1}" -f $name, $w) }
}

Apply-ToFile $WordsJson
Apply-ToFile $FallbackJson
Write-Host "[단어 필터 도구] 완료"
exit 0
