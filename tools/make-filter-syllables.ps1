<#
한 음절 포함 파일 만들기 (<261003_1.1>(5.1.1)) — OpenTyping.csproj 의 빌드 단계가 자동으로 실행한다.

  필터링 파일(OpenTyping\Resources\필터링_단어.txt)을 프로그램과 같은 규칙(OpenTyping\FilterWords.cs)으로 읽어,
  한 음절 필터링 단어(한 글자 항목)만 한 줄에 하나씩 적은 파일을 -OutFile 에 만든다. 이 파일만 실행 파일 안에
  포함된다 — 기존 단어 필터링 단어(두 글자 이상 항목)와 '#' 주석은 배포되는 실행 파일에 들어가지 않는다.

  필터링 파일이 올바른 UTF-8이 아니거나 문제 항목이 있으면, 어느 항목이 문제인지 알려 주고 실패(종료 코드 1)로
  끝난다 — 빌드도 그 자리에서 멈춘다(예전 내용이나 빈 내용이 조용히 들어가지 않게, <261003_1.1>(5.1.1.3)).
  내용이 그대로면 파일을 다시 쓰지 않는다.

사용 예
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-filter-syllables.ps1 -OutFile <txt>
    (검사용) ... -FilterFile <txt>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$FilterFile = ""
)

$ErrorActionPreference = "Stop"
$otp = Join-Path (Split-Path -Parent $PSScriptRoot) "OpenTyping"
$filterFileGiven = $FilterFile -ne ""
if ($FilterFile -eq "") { $FilterFile = Join-Path $otp "Resources\필터링_단어.txt" }

function Fail($msg) {
    Write-Host ("[한 음절 포함 파일] 실패: " + $msg)
    exit 1
}

try {
    Add-Type -Path (Join-Path $otp "FilterWords.cs")
} catch {
    Fail ("도구를 준비하지 못했습니다: " + $_.Exception.Message)
}

if (-not (Test-Path -LiteralPath $FilterFile)) {
    # 필터링 파일은 공개 저장소에 올리지 않는다(.gitignore). 저장소를 새로 받은 경우처럼 기본 위치에 없으면 함께 올린 예시 파일
    # (Resources\필터링_단어.example.txt, 한 음절만)로 대신해 빌드가 멈추지 않게 한다. -FilterFile 로 준 파일이 없으면 그대로 실패한다.
    $example = Join-Path $otp "Resources\필터링_단어.example.txt"
    if (-not $filterFileGiven -and (Test-Path -LiteralPath $example)) {
        Write-Host ("[한 음절 포함 파일] 참고: 필터링 파일이 없어 예시 파일로 대신합니다: " + $example)
        $FilterFile = $example
    } else { Fail "필터링 파일이 없습니다: $FilterFile" }
}
$bytes = [System.IO.File]::ReadAllBytes($FilterFile)
try {
    $filterText = (New-Object System.Text.UTF8Encoding($false, $true)).GetString($bytes)   # 올바르지 않은 바이트에서 예외
} catch {
    Fail ("필터링 파일이 올바른 UTF-8이 아닙니다(메모장이면 '다른 이름으로 저장'에서 인코딩을 UTF-8로): " + $FilterFile)
}

$entries = [OpenTyping.FilterWords]::Parse($filterText)
$problems = @($entries | Where-Object { $_.Problem -ne $null })
if ($problems.Count -gt 0) {
    foreach ($p in $problems) { Write-Host ("[한 음절 포함 파일] {0}번째 줄 '{1}': {2}" -f $p.Line, $p.Text, $p.Problem) }
    Fail ("필터링 파일에 문제 항목이 {0}개 있습니다: {1}" -f $problems.Count, $FilterFile)
}

# 실행 중 적용하는 것과 같은 기준(FilterWords.SyllablesOf — 한글 완성 음절 한 글자)으로 뽑고, 겹치는 것은 한 번만 적는다.
$syllables = [OpenTyping.FilterWords]::SyllablesOf($entries)
$seen = New-Object 'System.Collections.Generic.HashSet[char]'
$lines = New-Object 'System.Collections.Generic.List[string]'
foreach ($ch in $syllables.ToCharArray()) { if ($seen.Add($ch)) { $lines.Add([string]$ch) } }
$content = ($lines -join "`r`n") + "`r`n"

$dir = Split-Path -Parent $OutFile
if ($dir -ne "" -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$utf8 = New-Object System.Text.UTF8Encoding($false)
if ((Test-Path -LiteralPath $OutFile) -and ([System.IO.File]::ReadAllText($OutFile, $utf8) -ceq $content)) {
    # 내용이 같아도 파일 시각은 새로 해 둔다 — 그래야 MSBuild 가 이 단계를 '최신'으로 보고 다음 빌드부터 건너뛴다.
    try { [System.IO.File]::SetLastWriteTime($OutFile, [DateTime]::Now) } catch { }
    Write-Host ("[한 음절 포함 파일] 한 음절 {0}개 (그대로)" -f $lines.Count)
    exit 0
}
try {
    [System.IO.File]::WriteAllText($OutFile, $content, $utf8)
} catch { Fail ("한 음절 포함 파일을 쓰지 못했습니다(" + $OutFile + "): " + $_.Exception.Message) }
Write-Host ("[한 음절 포함 파일] 한 음절 {0}개를 썼습니다: {1}" -f $lines.Count, $OutFile)
exit 0
