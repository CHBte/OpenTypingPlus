; OpenTypingPlus 위치 선택형 SFX 설치 스크립트 (<260830_2-4> 전면 재구현)
; build-exe.bat이 이 파일을 ISCC.exe로 컴파일한다. 사람이 직접 컴파일하려면 Inno Setup IDE로
; 이 파일을 열고 컴파일(Ctrl+F9)하면 되며, 그 경우 아래 기본값(버전/닷넷 설치 파일 경로)이 쓰인다.
;
; 전제: build.bat을 먼저 실행해 "..\build\" 아래에 열린타자+.exe와
; layouts\ data\ wordslist\ stages\ hands\ 폴더가 이미 만들어져 있어야 한다.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

#define MyAppName "Open Typing Plus"
#define MyAppExeName "열린타자+.exe"
#define DotNetInstallerFileName "windowsdesktop-runtime-10.0.11-win-x64.exe"

#ifndef DotNetInstallerPath
  #define DotNetInstallerPath "..\OpenTyping\obj\installer-cache\" + DotNetInstallerFileName
#endif

; <261005_2>(2) 이번 설치 파일에 든 stages·layouts·hands 의 파일 목록(하위 폴더 포함, 설치 폴더 기준 상대 경로)을
; 컴파일할 때 만들어 [Code] 의 InstallManifest 상수로 넣는다. 같은 폴더에 다시 설치할 때, 이 목록에 없는 파일
; ('남는 파일')을 찾는 데 쓴다. 아래 [Files] 의 stages·layouts·hands 항목과 같은 폴더를 본다.
; 항목은 '|'로 잇고 끝에도 '|'를 둔다. 파일 이름의 작은따옴표는 Pascal 문자열 안이므로 두 번 적는다.
#define BuildDir AddBackslash(SourcePath) + "..\build"
#define ScanDir(str Rel) ScanLoop(FindFirst(BuildDir + "\" + Rel + "\*", faAnyFile), Rel)
#define ScanLoop(int H, str Rel) \
  H == 0 ? "" : ScanEntry(H, Rel, FindGetFileName(H)) + (FindNext(H) ? ScanLoop(H, Rel) : (FindClose(H), ""))
#define ScanEntry(int H, str Rel, str Name) \
  (Name == "." || Name == "..") ? "" : \
  (DirExists(BuildDir + "\" + Rel + "\" + Name) ? ScanDir(Rel + "\" + Name) : (StringChange(Rel + "\" + Name, "'", "''") + "|"))
#define InstallManifest ScanDir("stages") + ScanDir("layouts") + ScanDir("hands")

[Setup]
; 이 GUID는 고정값이다. 절대 바꾸지 말 것 — 바뀌면 Windows가 다른 프로그램으로 인식한다.
AppId={{6213C516-00A9-46B3-A63B-4671D3E53731}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Gear
; <260830_2-4> (4)/(4-1): 바탕화면 OTP 폴더 존재 여부를 우리가 직접 검사·분기하므로
; Inno 표준 "대상 위치 선택" 페이지는 끄고, 최종 경로는 [Code]에서 WizardForm.DirEdit.Text로
; 프로그램적으로 정한다. DefaultDirName은 그 초기값(=충돌 없을 때 실제로 쓰이는 기본값)이다.
DefaultDirName={autodesktop}\OTP
DisableDirPage=yes
DefaultGroupName=
DisableProgramGroupPage=yes
DisableWelcomePage=no
; 바탕화면 등 사용자 소유 위치에 설치하므로 관리자 권한이 필요 없다.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\build-exe
OutputBaseFilename=OpenTypingPlus_Setup
Compression=lzma2/max
SolidCompression=yes
LicenseFile=license_summary.txt
UsedUserAreasWarning=no
WizardStyle=modern
SetupIconFile=..\OpenTyping\icon.ico
; <260830_2-2> 포터블 설치 취지에 맞춰 Windows "프로그램 추가/제거" 목록에는 등록하지 않는다.
; (제거는 OTP 폴더 안의 "설치삭제" 폴더 안 "설치삭제.exe"를 직접 실행하는 방식으로만 한다.
; <260831_1> 참고.) 이 값을 꺼도 삭제 프로그램(unins000.exe) 자체는 그대로 생성된다 —
; 레지스트리 등록만 안 할 뿐이다.
CreateUninstallRegKey=no
; <260831_1>: 삭제 프로그램(설치삭제.exe/.dat, 원래 unins000.exe/.dat)을 OTP 폴더 바로
; 아래가 아니라 그 안의 "설치삭제" 하위 폴더에 두도록 지정한다. RenameUninstaller()가 이
; 폴더 안에서 개명하며, 삭제 시 Inno가 이 폴더 자체도 함께 정리한다(UninstallFilesDir이
; {app}과 다르면 자동으로 지워짐 — Inno Setup 공식 문서 기준. 실제 삭제 동작은 아직 이
; 컴퓨터에서 직접 재현하지 못했으므로, 다음 위치 선택형 SFX 삭제 테스트 때 재확인할 것).
UninstallFilesDir={app}\설치삭제

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "..\build\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\layouts\*"; DestDir: "{app}\layouts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs createallsubdirs
; <260830_2-3>(4-1): words.json은 {app}\wordslist\에 넣지 않는다 — AppData(Roaming)로 대신 들어가고,
; {app}\wordslist\에는 [Icons]로 그 파일의 바로가기만 생긴다. build\wordslist\ 폴더 안에는 원래 words.json
; 하나뿐이라(build.bat 참고), 그걸 빼면 복사할 게 없어 wordslist\* 와일드카드 자체를 없앴다 — 와일드카드가
; 아무것도 못 찾으면 Inno Setup이 컴파일 에러로 취급하기 때문이다(실제로 컴파일해서 확인함).
Source: "..\build\stages\*"; DestDir: "{app}\stages"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\hands\*"; DestDir: "{app}\hands"; Flags: ignoreversion recursesubdirs createallsubdirs
; <260830_2-3>(4-1): words.json의 실제 라이브 사본은 AppData\Roaming\OTP\OpenTypingPlus\wordslist\에 둔다.
; (4-1-1)/(4-1-2)에서 사용자가 "기존의 단어 목록 계속 사용(유지)"를 고르면 ShouldInstallWordsJson이 False가 되어
; 이 항목을 건너뛴다.
Source: "..\build\wordslist\words.json"; DestDir: "{userappdata}\OTP\OpenTypingPlus\wordslist"; Flags: ignoreversion; Check: ShouldInstallWordsJson
; words.json은 {tmp}에도 항상(조건 없이) 꺼내 둔다 — (4-1-1) 비교와 words-original.json 생성에
; "이번 설치 패키지가 원래 기본값으로 담고 있는 내용"이 필요한데, 그건 사용자가 "기존의 단어 목록 계속
; 사용(유지)"를 골라 위 항목이 실제로 안 깔릴 수도 있어 그것만으론 알 수 없기 때문이다.
Source: "..\build\wordslist\words.json"; DestDir: "{tmp}"; Flags: dontcopy
; .NET 런타임 설치 파일은 SFX 안에 압축 포함만 하고(dontcopy), 실제로 필요할 때만
; ExtractTemporaryFile로 {tmp}에 꺼낸다 (아래 [Code]의 CurStepChanged 참고).
Source: "{#DotNetInstallerPath}"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
; <260830_2-3>(4-1): 사용자가 AppData에 직접 갈 필요 없이, OTP 폴더 안에서 바로 단어 목록을
; 편집할 수 있게 하는 바로가기. 대상이 AppData 쪽 실제 파일이라 사용자가 더블클릭하면 그 파일이 열린다.
; <261003_1.1>: 이름을 '단어 목록 편집 (words.json)'에서 '단어 목록 편집(words.json)'으로 바꿨다.
Name: "{app}\wordslist\단어 목록 편집(words.json)"; Filename: "{userappdata}\OTP\OpenTypingPlus\wordslist\words.json"

[InstallDelete]
; <261003_1.1>: 예전 이름의 바로가기가 남아 있으면 지운다(덮어쓰기 설치는 같은 이름의 파일만 바꾸므로 그냥 두면
; 두 개가 남는다). 정확히 이 이름의 바로가기 파일(.lnk) 하나만 지우고(files 형식은 폴더를 지우지 않는다),
; 지우지 못해도 설치는 그대로 진행된다(Inno 의 InstallDelete 는 실패해도 설치를 멈추지 않는다).
Type: files; Name: "{app}\wordslist\단어 목록 편집 (words.json).lnk"

[Run]
; (1)/(1-1): .NET 10 Desktop Runtime(x64)이 없을 때만 마이크로소프트 공식 설치 창을 그대로 띄운다.
; 무음 옵션을 주지 않으므로 설치 창이 그대로 표시되고, 끝날 때까지 기다린 뒤 다음 단계로 넘어간다.
Filename: "{tmp}\{#DotNetInstallerFileName}"; StatusMsg: ".NET 10 Desktop Runtime 설치 창을 여는 중..."; Check: NeedsDotNetRuntime; Flags: waituntilterminated
; (6): 설치 완료 후 "Open Typing Plus 실행" 여부 - 완료 페이지의 체크박스(기본 선택됨)로 구현한다.
; 체크된 채로 "마침"을 누르면 실행, 체크 해제 후 누르면 실행하지 않는다.
; <260901_2>: 원래는 postinstall 플래그로 Inno이 자동 생성하는 체크박스를 썼는데, 그 체크박스는
; Inno의 TNewCheckListBox가 레거시 OBM_CHECKBOXES 비트맵(고정 픽셀, DPI 무관)으로 직접 그려서
; 200% 화면 배율에서 가로가 눌려 잘려 보였다(Inno Setup 소스 코드로 원인 확인 — Components\
; NewCheckListBox.pas의 FCheckWidth/FCheckHeight 계산에 ScaleX/ScaleY가 전혀 없음. Inno 자체
; 내부 컨트롤이라 우리 스크립트에서 고칠 수 없다). 반면 TNewCheckBox는 진짜 네이티브 Win32
; 체크박스 컨트롤이라 Windows 테마 엔진이 알아서 DPI에 맞게 그린다. 그래서 이 postinstall
; 항목은 없애고, 아래 CurPageChanged/DeinitializeSetup에서 직접 만든 TNewCheckBox +
; Exec 조합으로 같은 동작을 재현한다.

[UninstallDelete]
; <260830_2-2> "설치삭제.exe"(원래 unins000.exe)를 실행하면, 설치 때 넣은 파일들 외에도
; AppData의 OTP\OpenTypingPlus 폴더(로밍/로컬 둘 다)를 통째로 삭제한다.
; words.json의 예외 처리는 아래 [Code]의 InitializeUninstall이 이 삭제가 실행되기 전에 바탕화면에
; 사본을 만들어 두는 방식으로 처리한다 (<260830_2-5>).
Type: filesandordirs; Name: "{userappdata}\OTP\OpenTypingPlus"
Type: filesandordirs; Name: "{localappdata}\OTP\OpenTypingPlus"
; <261003_1.1>: 바로가기는 옛 이름과 새 이름 모두, 정확히 그 이름의 바로가기 파일만 지운다.
Type: files; Name: "{app}\wordslist\단어 목록 편집 (words.json).lnk"
Type: files; Name: "{app}\wordslist\단어 목록 편집(words.json).lnk"

[Code]
var
  { <260830_2-4>(4): 최종 설치 위치. 기본값은 DefaultDirName과 같은 바탕화면\OTP. }
  FinalInstallDir: String;
  { <260830_2-4>(4-1-2)·<261003_1.1>(7.3): "기존의 단어 목록 계속 사용(유지)"를 골랐으면 True — words.json을
    AppData에 새로 안 쓰고 이미 있는 걸 그대로 둔다. (7.5.1)에서 "고친 기존의 단어 목록 계속 사용하기"를 골라도 True.
    KeepChosen 은 (7.3) 창에서 고른 값 자체 — [설치]를 누른 뒤의 처리(PrepareToInstall)는 이 값에서 다시 시작한다. }
  KeepWordsJson: Boolean;
  KeepChosen: Boolean;
  { <261003_1.1>(7.3.1)·<261005_1>: 사용자가 이미 취소를 고른 경우 Inno 기본 "설치를 종료하시겠습니까?"를 다시
    묻지 않고 바로 끝내기 위한 표시(CancelButtonClick 참고). }
  SilentCancel: Boolean;
  { CurStepChanged(ssInstall) — 실제 설치(파일 복사·AppData 정리)가 시작됐는가 / ssPostInstall 까지 갔는가. }
  InstallStarted: Boolean;
  PostInstallReached: Boolean;
  { <261005_2>(7.2) 남는 파일 중 지우는 도중에 지우지 못한 파일(설치가 끝나면 알린다). }
  LeftoverDeleteFailed: String;
  { <260830_2-4>(3-1)/(3-2)에서 "취소"를 고르면 True — 이후 모든 단계를 건너뛴다. }
  InstallCancelled: Boolean;
  { <260831 검토>(3-1)의 삭제 예약. 예전엔 프롬프트에서 곧바로 DelTree를 했는데, 그 시점은
    아직 사용자가 준비 완료 페이지에서 "설치"를 누르기 전이라, 거기서 취소하거나 설치가 실패하면
    "설치는 안 됐는데 기존 폴더·데이터는 이미 사라진" 상태가 됐다. 이제 선택만 여기 기억해 두고
    실제 삭제는 설치가 확정된 뒤(ssInstall)에 한다.
    (<260927_15.1>: 바탕화면 OTP 폴더 쪽은 더는 통째로 지우지 않고 덮어쓰기 설치로 바뀌어서
    이런 예약이 필요 없다 — 아래 PendingDesktopDelete가 없어진 이유.) }
  PendingRoamingCleanup: Boolean;
  PendingLocalCleanup: Boolean;
  { (3-1)에서 고른 값 자체 — PrepareToInstall 이 다시 시작할 때 PendingRoamingCleanup 을 이 값으로 되돌린다. }
  RoamingCleanupChosen: Boolean;
  // <260831 2차 검토>·<261003_1.1>(7.3.2): "기존의 단어 목록 계속 사용(유지)"를 고른 words.json의 임시 사본
  // 폴더(바탕화면 OTP_words_tmp_(날짜시각)). Inno의 임시 폴더(tmp 상수)에 두면 설치가 중간에 취소·실패했을 때
  // Inno가 그 폴더를 통째로 지우면서 사본까지 함께 사라져(원본은 이미 DelTree로 없어진 뒤) 데이터가 영구
  // 소실된다. 그래서 설치가 끝나도 남는 바탕화면에 둔다. 되돌리기까지 성공하면 ssDone에서 지우고, 실패하면
  // 그대로 남겨 둔다. 이번 설치에서 만든 폴더일 때만 값이 있다.
  PreservedBackupDir: String;
  PreservedRestoreOk: Boolean;
  { <261003_1>(7.5): 사용자가 고친 단어 목록이 있는데 '단계 개정판 번호'가 달라 유지할 수 없을 때 True.
    새 목록을 설치하고, 고친 목록은 바탕화면 사본 폴더(StageMismatchBackupDir, OTP_words_보존_(날짜시각))로 남긴다.
    StageMismatchBackupDir 은 이번 설치에서 만든 폴더일 때만 값이 있다. }
  StageVersionMismatch: Boolean;
  StageMismatchBackupDir: String;
  { <260901_2>: 완료 페이지의 "Open Typing Plus 실행" 체크박스. Inno이 자동 생성하는
    TNewCheckListBox 대신 우리가 직접 만드는 진짜 TNewCheckBox(네이티브 Win32 체크박스라
    DPI에 맞게 그려짐)다. LaunchAfterFinish는 그 체크 상태를 기억해 뒀다가, 폼이 이미 닫힌 뒤
    실행되는 DeinitializeSetup에서 읽는다(그 시점엔 LaunchCheckBox 객체 자체가 이미 해제됐을
    수 있어 컨트롤이 아니라 이 변수를 믿는다). }
  LaunchCheckBox: TNewCheckBox;
  LaunchAfterFinish: Boolean;
  { 같은 폴더에 다시 설치할 때 예전 설치삭제.exe/.dat 를 unins000.exe/.dat 로 되돌려 놓았으면 True
    (PrepareUninstallerForAppend 참고). UninstallerNameDone 은 ssDone 에서 이름 바꾸기를 마쳤는가. }
  UninstallerRestoredForAppend: Boolean;
  UninstallerNameDone: Boolean;

const
  OtpAppGuid = '6213C516-00A9-46B3-A63B-4671D3E53731';
  MarkerFileName = '.otp-identity';
  { <261005_2>(2) 이번 설치 파일에 든 stages·layouts·hands 파일 목록(위 전처리기 InstallManifest 참고). }
  InstallManifest = '{#InstallManifest}';
  CancelMessage = '설치를 취소합니다.';

#include "install_helpers.iss"

{ ============== (1) .NET 10.0 Desktop Runtime 감지 ============== }
{ 실제로 이 개발 컴퓨터의 레지스트리를 직접 조회해 확인한 위치를 1순위로 쓰고,
  혹시 다른 설치 경로로 인해 값이 다른 곳에 있을 경우에 대비해 네이티브 64비트 경로도 보조로 검사한다. }
function ValueNamesHaveVersion10(RootKey: Integer; SubKeyName: String): Boolean;
var
  ValueNames: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey, SubKeyName, ValueNames) then
  begin
    for I := 0 to GetArrayLength(ValueNames) - 1 do
    begin
      if Copy(ValueNames[I], 1, 3) = '10.' then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function IsDotNetDesktopRuntime10Installed(): Boolean;
begin
  { 실제 확인된 경로 (2026-08-30, 이 컴퓨터에서 직접 재현): WOW6432Node 아래에 값 이름으로 기록됨. }
  Result := ValueNamesHaveVersion10(HKLM64,
    'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');
  if not Result then
    { 보조 경로: 네이티브 64비트 위치에 기록되는 경우를 대비 }
    Result := ValueNamesHaveVersion10(HKLM64,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');
end;

function NeedsDotNetRuntime(): Boolean;
begin
  Result := not IsDotNetDesktopRuntime10Installed();
end;

{ ============== 공통: 신원 마커 읽기/쓰기 ============== }
function HasOtpMarker(FolderPath: String): Boolean;
var
  Content: AnsiString;
begin
  Result := False;
  if FileExists(FolderPath + '\' + MarkerFileName) then
  begin
    if LoadStringFromFile(FolderPath + '\' + MarkerFileName, Content) then
      Result := (Trim(String(Content)) = OtpAppGuid);
  end;
end;

procedure WriteOtpMarker(FolderPath: String);
begin
  if not DirExists(FolderPath) then
    ForceDirectories(FolderPath);
  SaveStringToFile(FolderPath + '\' + MarkerFileName, OtpAppGuid, False);
end;

{ ============== (3) AppData 신원 확인 ============== }
// <260901_1>: 안내 문구에 "%USERPROFILE%"을 문자 그대로 보여주면 뜻을 모르는 사용자가 있을 수
// 있어, 설치 중인 이 컴퓨터의 실제 경로로 바꿔 보여준다. userappdata 상수는 "...\AppData\
// Roaming"까지 포함하므로 그 부모 폴더(ExtractFileDir)가 "...\AppData"다.
// (참고: 이 설명에 상수 이름을 중괄호로 감싸 그대로 적으면 { } 블록 주석을 조기에 닫아버려
// 컴파일 에러가 난다 — RenameUninstaller 위 <260831_1> 주석에서 실제로 겪음. 그래서 // 줄
// 주석으로 쓴다.)
function AppDataRootFolder(): String;
begin
  Result := ExtractFileDir(ExpandConstant('{userappdata}'));
end;

function RoamingOtpFolder(): String;
begin
  Result := ExpandConstant('{userappdata}\OTP\OpenTypingPlus');
end;

function LocalOtpFolder(): String;
begin
  Result := ExpandConstant('{localappdata}\OTP\OpenTypingPlus');
end;

{ <260830_2-4-3>(2)/<260830_2-5-1>/<260830_3>: 버튼이 캡션 문구보다 좁아 글자가 잘리는 문제를
  공식 문서가 권장하는 방식으로 해결한다 — TNewButton엔 Canvas가 없어(공식 문서로 확인함),
  임시 TBitmap의 Canvas에 같은 글꼴을 얹어 실제 렌더 너비를 잰다. HPadding=버튼 테두리 안쪽
  좌우 여백 합계, MinWidth=그보다 좁아지지 않을 최소값. }
function TextButtonWidth(AFont: TFont; const ACaption: String; HPadding, MinWidth: Integer): Integer;
var
  Bmp: TBitmap;
begin
  Bmp := TBitmap.Create;
  try
    { Canvas.Font 자체는 읽기 전용이라(컴파일해서 확인함 — "Read-only property") 통째로 대입할
      수 없다. 개별 속성만 옮겨 적는다. }
    Bmp.Canvas.Font.Name := AFont.Name;
    Bmp.Canvas.Font.Size := AFont.Size;
    Bmp.Canvas.Font.Style := AFont.Style;
    Result := Bmp.Canvas.TextWidth(ACaption) + HPadding;
  finally
    Bmp.Free;
  end;
  if Result < MinWidth then Result := MinWidth;
end;

{ <260830_2-4-3-2>(1): 하드코딩한 라벨 Height 추측이 실제 렌더링과 어긋나 마지막 줄이
  가려져 잘리는 사고가 실제로 났다(ShowDesktopConflictPrompt, <260830_2-4-3-2>(2)). 위
  TextButtonWidth로 버튼 폭을 실측하는 것과 같은 원리를, 여러 줄 라벨의 폭·높이에도 적용해
  하드코딩 추측을 없앤다. 각 캡션은 WordWrap의 자동 줄바꿈에 기대지 않고 #13#10으로 줄을
  직접 나눠 넣으므로(어디서 끊길지 미리 알 수 없는 자동 줄바꿈은 오히려 잘림 사고의 원인),
  "줄 수 × 한 줄 높이"로 정확한 라벨 높이를 구할 수 있다 — 단, 각 줄이 라벨 폭 안에 실제로
  들어가야 이 전제가 성립하므로 EnsureFormWideEnoughForLabel로 폭도 함께 보장한다. }

{ 한 줄의 실제 렌더 높이(px). }
function TextLineHeight(AFont: TFont): Integer;
var
  Bmp: TBitmap;
begin
  Bmp := TBitmap.Create;
  try
    Bmp.Canvas.Font.Name := AFont.Name;
    Bmp.Canvas.Font.Size := AFont.Size;
    Bmp.Canvas.Font.Style := AFont.Style;
    Result := Bmp.Canvas.TextHeight('Ag가');
  finally
    Bmp.Free;
  end;
end;

{ ACaption을 #13#10 기준으로 나눈 줄 수. }
function TextLineCount(const ACaption: String): Integer;
var
  Lines: TStringList;
begin
  Lines := TStringList.Create;
  try
    Lines.Text := ACaption;
    Result := Lines.Count;
  finally
    Lines.Free;
  end;
end;

{ ACaption의 각 줄(#13#10 기준) 중 가장 넓은 줄의 실제 렌더 폭(px). TextButtonWidth를
  HPadding=0, MinWidth=0으로 불러 버튼 여백 없이 순수 텍스트 폭만 재는 방식을 재사용한다. }
function TextWidestLine(AFont: TFont; const ACaption: String): Integer;
var
  Lines: TStringList;
  I, W: Integer;
begin
  Result := 0;
  Lines := TStringList.Create;
  try
    Lines.Text := ACaption;
    for I := 0 to Lines.Count - 1 do
    begin
      W := TextButtonWidth(AFont, Lines[I], 0, 0);
      if W > Result then Result := W;
    end;
  finally
    Lines.Free;
  end;
end;

{ AForm의 클라이언트 폭이 ACaption의 가장 넓은 줄 + HMargin(좌우 여백 합계)보다 좁으면 그만큼
  넓힌다. 이러면 그 줄이 라벨 폭 안에서 다시 자동 줄바꿈되는 일이 없어, TextLineCount로 구한
  줄 수와 실제 렌더 줄 수가 어긋나지 않는다. }
procedure EnsureFormWideEnoughForLabel(AForm: TSetupForm; AFont: TFont; const ACaption: String; HMargin: Integer);
var
  Needed: Integer;
begin
  Needed := TextWidestLine(AFont, ACaption) + HMargin;
  if Needed > AForm.ClientWidth then
    AForm.ClientWidth := Needed;
end;

{ <261003_1.1>: 두 버튼을 라벨 아래 TopY 부터 놓는다. 한 줄(오른쪽 정렬, B1 이 왼쪽·B2 가 오른쪽)에 들어가면 좌/우로,
  창 너비를 넘길 것 같으면 상/하(B1 이 위)로 둔다. 상/하일 때는 두 버튼의 너비를 넓은 쪽에 맞춘다.
  돌려주는 값은 버튼들의 맨 아래 y. 폼 높이는 부르는 쪽이 이 값에 맞춘다. }
function LayoutTwoButtons(AForm: TSetupForm; B1, B2: TNewButton; TopY: Integer): Integer;
var
  W: Integer;
begin
  if B1.Width + 8 + B2.Width <= AForm.ClientWidth - 32 then
  begin
    B1.Top := TopY;
    B2.Top := TopY;
    B2.Left := AForm.ClientWidth - 16 - B2.Width;
    B1.Left := B2.Left - 8 - B1.Width;
    Result := TopY + B1.Height;
  end
  else
  begin
    W := B1.Width;
    if B2.Width > W then W := B2.Width;
    if W > AForm.ClientWidth - 32 then W := AForm.ClientWidth - 32;
    B1.Width := W;
    B2.Width := W;
    B1.Left := AForm.ClientWidth - 16 - W;
    B2.Left := B1.Left;
    B1.Top := TopY;
    B2.Top := TopY + B1.Height + 6;
    Result := B2.Top + B2.Height;
  end;
end;

{ 화면 밖에 놓는 보이지 않는 버튼. 버튼 두 개 중 어느 것도 [Esc]에 대응하지 않는 창에서, [Esc]를 누르면
  ModalResult = mrCancel 로 창이 닫히게 한다(창 닫기 X 와 같은 결과). VCL 은 보이는(Visible) 버튼만 [Esc]에
  반응하므로 숨기지 않고 화면 밖 좌표에 둔다. }
procedure AddEscCancelButton(AForm: TSetupForm);
var
  B: TNewButton;
begin
  B := TNewButton.Create(AForm);
  B.Parent := AForm;
  B.Left := -1000;
  B.Top := -1000;
  B.Width := 10;
  B.Height := 10;
  B.TabStop := False;
  B.Cancel := True;
  B.ModalResult := mrCancel;
end;

{ <261003_1.1>(7.3)(7.5)(7.7) 바탕화면에 BaseName 폴더를 새로 만든다. 같은 이름의 폴더나 파일(확장자가 없는
  파일)이 이미 있으면 ' (2)'부터 차례로 아직 없는 번호를 붙인다. 만든 폴더의 전체 경로를 돌려주고, 만들지
  못하면 '' 를 돌려준다(그때 Attempted 에는 만들려던 경로가 들어 있다). }
function CreateUniqueDesktopDir(const BaseName: String; var Attempted: String): String;
var
  Desktop: String;
  N: Integer;
begin
  Result := '';
  Desktop := ExpandConstant('{userdesktop}');
  N := 1;
  repeat
    Attempted := AddBackslash(Desktop) + NumberedName(BaseName, N);
    N := N + 1;
  until (not DirExists(Attempted) and not FileExists(Attempted)) or (N > 1000);
  if DirExists(Attempted) or FileExists(Attempted) then Exit;
  if ForceDirectories(Attempted) then
    Result := Attempted;
end;

{ 바탕화면 폴더를 만들고 그 안에 words.json 사본을 만든다. 성공하면 폴더 경로, 실패하면 '' 를 돌려준다.
  폴더는 만들었는데 사본 복사에 실패하면 이번에 만든 그 빈 폴더를 지운다(<261003_1.1>(7.3.2.3)(7.5.1.6.1)(7.7)).
  Attempted 에는 만들려던(또는 만든) 폴더 경로가 들어간다. }
function CopyWordsJsonToNewDesktopDir(const BaseName, LivePath: String; var Attempted: String): String;
var
  Dir: String;
begin
  Result := '';
  Dir := CreateUniqueDesktopDir(BaseName, Attempted);
  if Dir = '' then Exit;
  if FileCopy(LivePath, Dir + '\words.json', False) then
    Result := Dir
  else
  begin
    { 이번에 새로 만든 폴더라 그 안에는 복사하다 만 사본 말고는 없다. }
    DeleteFile(Dir + '\words.json');
    RemoveDir(Dir); { 비어 있을 때만 지워진다 }
  end;
end;

{ (3-1): 신원이 확인된 흔적을 지울지 묻는다. "예" 외엔(아니오/X) 전부 설치 취소. }
procedure ShowAppDataCleanupPrompt();
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  YesButton, NoButton: TNewButton;
  NeededHeight: Integer;
begin
  ConfirmForm := CreateCustomForm(380, 190, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      AppDataRootFolder() + '에' + #13#10 +
      '이미 Open Typing Plus이 실행되었던 흔적이 있습니다.' + #13#10 +
      '관련 폴더와 파일을 삭제합니다.' + #13#10 +
      '(이미 실행했던 Open Typing Plus 폴더 및 파일만 삭제하고' + #13#10 +
      '다른 것은 삭제하지 않습니다.)';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    NoButton := TNewButton.Create(ConfirmForm);
    NoButton.Parent := ConfirmForm;
    NoButton.Caption := '아니오(설치 취소)';
    NoButton.Height := WizardForm.CancelButton.Height;
    NoButton.Width := TextButtonWidth(NoButton.Font, NoButton.Caption, 40, 75);
    { 라벨의 실측 높이가 원래 여유 공간보다 클 수 있으니, 버튼 행과 겹치지 않게 폼 높이를
      필요하면 늘린다. 버튼은 ClientHeight 기준 상대 좌표라 늘어나면 자동으로 같이 밀려난다. }
    NeededHeight := MsgLabel.Top + MsgLabel.Height + 16 + NoButton.Height + 10;
    if NeededHeight > ConfirmForm.ClientHeight then
      ConfirmForm.ClientHeight := NeededHeight;
    NoButton.Top := ConfirmForm.ClientHeight - NoButton.Height - 10;
    NoButton.Left := ConfirmForm.ClientWidth - NoButton.Width - 16;
    NoButton.ModalResult := mrNo;
    NoButton.Cancel := True;

    YesButton := TNewButton.Create(ConfirmForm);
    YesButton.Parent := ConfirmForm;
    YesButton.Caption := '예';
    YesButton.Height := WizardForm.CancelButton.Height;
    YesButton.Width := TextButtonWidth(YesButton.Font, YesButton.Caption, 40, 75);
    YesButton.Top := NoButton.Top;
    YesButton.Left := NoButton.Left - YesButton.Width - 8;
    YesButton.ModalResult := mrYes;
    YesButton.Default := True;
    ConfirmForm.ActiveControl := YesButton;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    if ConfirmForm.ShowModal() = mrYes then
    begin
      { <260831 검토>: 예전엔 마커 유무와 무관하게 두 폴더를 다 지워서, 마커 없는(=남의 것일 수도
        있는) 폴더까지 (3-2)의 "위험 감수" 동의 없이 삭제됐다 — 위 문구의 "Open Typing Plus 폴더
        및 파일만 삭제"와도 어긋났다. 신원이 확인된 폴더만 삭제 대상으로 예약한다. }
      PendingRoamingCleanup := DirExists(RoamingOtpFolder()) and HasOtpMarker(RoamingOtpFolder());
      PendingLocalCleanup := DirExists(LocalOtpFolder()) and HasOtpMarker(LocalOtpFolder());
    end
    else
      InstallCancelled := True;
  finally
    ConfirmForm.Free();
  end;
end;

{ (3-2): 이름만 같은 남의 폴더로 보이는 경우. 폴더를 열어 보여주고, 위험을 감수할지 취소할지 묻는다. }
procedure ShowAppDataCollisionPrompt();
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  ListLabel: TNewStaticText;
  FileListMemo: TNewMemo;
  RiskButton, CancelBtn: TNewButton;
  ErrorCode: Integer;
  NeededHeight: Integer;
begin
  { <260830_3>: CancelBtn 캡션이 길어(약 27자) 기존 460 너비에선 다른 버튼과 겹치거나 폼 밖으로
    나갈 수 있어 여유 있게 넓힌다. }
  ConfirmForm := CreateCustomForm(540, 430, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      AppDataRootFolder() + '에 우연히 같은 이름만 같은' + #13#10 +
      '(Open Typing Plus의 고유 식별자가 없는) 다른 프로그램이 설치되어 있습니다.' + #13#10 +
      '폴더 이름을 변경할 수 없어 같은 폴더 안에 기존 파일과 지금 설치하려는 파일이 같은 폴더' + #13#10 +
      '안에 있게 됩니다. 심지어 지금 설치하려는 파일과 이름이 같은 파일이 이미 있다면, 기존' + #13#10 +
      '파일이 삭제됩니다. 또한 나중에 이 프로그램을 또 설치할 시에 이 폴더는 삭제됩니다.' + #13#10 +
      '해당 폴더(들)을 띄워드립니다. 폴더 안의 내용을 보시고 판단하세요.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    ListLabel := TNewStaticText.Create(ConfirmForm);
    ListLabel.Parent := ConfirmForm;
    ListLabel.Left := 16;
    ListLabel.Top := MsgLabel.Top + MsgLabel.Height + 8;
    ListLabel.Caption := 'AppData 안의 폴더에 지금 설치하려는 파일 목록';

    FileListMemo := TNewMemo.Create(ConfirmForm);
    FileListMemo.Parent := ConfirmForm;
    FileListMemo.Left := 16;
    FileListMemo.Top := ListLabel.Top + ListLabel.Height + 4;
    FileListMemo.Width := ConfirmForm.ClientWidth - 32;
    FileListMemo.Height := 130;
    FileListMemo.ReadOnly := True;
    FileListMemo.ScrollBars := ssVertical;
    FileListMemo.Lines.Text :=
      'key_layout_data.json' + #13#10 +
      'stage_records_ko.json' + #13#10 + 'stage_records_en.json' + #13#10 +
      'DubeolsikStandard.json' + #13#10 + 'Dvorak.json' + #13#10 +
      'Qwerty.json' + #13#10 + 'Sebeolsik390.json' + #13#10 +
      'game_records_ko.json' + #13#10 + 'game_records_en.json' + #13#10 +
      'user_settings.json' + #13#10 +
      'words-original.json' + #13#10 + 'words.json' + #13#10 +
      '권리장전.json' + #13#10 + '대한민국 헌법 전문.json' + #13#10 +
      '명언 모음.json' + #13#10 + '미합중국 수정헌법.json' + #13#10 +
      '별 헤는 밤.json' + #13#10 + '세계 인권 선언.json' + #13#10 +
      '애국가.json' + #13#10 + '자유 소프트웨어는 정말 중요하다.json' + #13#10 +
      '히포크라테스 선서.json';

    { 라벨이 실측 높이만큼 늘어나 그 아래 목록·버튼과 겹치지 않게, 필요하면 폼 높이를 늘린다.
      버튼은 ClientHeight 기준 상대 좌표라 자동으로 같이 밀려난다. }
    NeededHeight := FileListMemo.Top + FileListMemo.Height + 16 +
      WizardForm.CancelButton.Height + WizardForm.CancelButton.Height + 6 + 10;
    if NeededHeight > ConfirmForm.ClientHeight then
      ConfirmForm.ClientHeight := NeededHeight;

    CancelBtn := TNewButton.Create(ConfirmForm);
    CancelBtn.Parent := ConfirmForm;
    CancelBtn.Caption := '설치 취소(이 버튼을 누르기 전에 선생님에게 물어보세요.)';
    CancelBtn.Height := WizardForm.CancelButton.Height;
    CancelBtn.Width := TextButtonWidth(CancelBtn.Font, CancelBtn.Caption, 40, 75);
    CancelBtn.Top := ConfirmForm.ClientHeight - CancelBtn.Height - 10;
    CancelBtn.Left := ConfirmForm.ClientWidth - CancelBtn.Width - 16;
    CancelBtn.ModalResult := mrCancel;
    CancelBtn.Cancel := True;

    RiskButton := TNewButton.Create(ConfirmForm);
    RiskButton.Parent := ConfirmForm;
    RiskButton.Caption := '위험을 감수하고 설치';
    RiskButton.Height := WizardForm.CancelButton.Height;
    RiskButton.Width := TextButtonWidth(RiskButton.Font, RiskButton.Caption, 40, 75);
    RiskButton.Top := CancelBtn.Top - RiskButton.Height - 6;
    RiskButton.Left := ConfirmForm.ClientWidth - RiskButton.Width - 16;
    RiskButton.ModalResult := mrOk;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    { 폴더(들)를 실제로 띄워준다. 로밍/로컬 둘 다 존재 확인 후 각각 연다. }
    if DirExists(RoamingOtpFolder()) and not HasOtpMarker(RoamingOtpFolder()) then
      ShellExecAsOriginalUser('open', RoamingOtpFolder(), '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    if DirExists(LocalOtpFolder()) and not HasOtpMarker(LocalOtpFolder()) then
      ShellExecAsOriginalUser('open', LocalOtpFolder(), '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);

    if ConfirmForm.ShowModal() <> mrOk then
      InstallCancelled := True;
  finally
    ConfirmForm.Free();
  end;
end;

{ (3): 위 두 창을 상황에 맞게 실행 }
procedure CheckAppDataIdentity();
var
  RoamingExists, LocalExists, RoamingMarked, LocalMarked: Boolean;
  MarkedFolderExists, UnmarkedFolderExists: Boolean;
begin
  RoamingExists := DirExists(RoamingOtpFolder());
  LocalExists := DirExists(LocalOtpFolder());
  RoamingMarked := RoamingExists and HasOtpMarker(RoamingOtpFolder());
  LocalMarked := LocalExists and HasOtpMarker(LocalOtpFolder());

  MarkedFolderExists := RoamingMarked or LocalMarked;
  UnmarkedFolderExists := (RoamingExists and not RoamingMarked) or (LocalExists and not LocalMarked);

  if MarkedFolderExists then
  begin
    ShowAppDataCleanupPrompt();
    if InstallCancelled then Exit;
  end;
  if UnmarkedFolderExists then
  begin
    ShowAppDataCollisionPrompt();
    if InstallCancelled then Exit;
  end;
end;

{ ============== (4)/(4-1) 바탕화면 OTP 폴더 충돌 처리 ============== }
function DesktopOtpHasFiles(): Boolean;
var
  FindRec: TFindRec;
  Path: String;
begin
  Result := False;
  Path := ExpandConstant('{autodesktop}\OTP');
  if not DirExists(Path) then Exit;
  if FindFirst(Path + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function PathLooksValid(Path: String): Boolean;
begin
  { 아주 단순한 검사: 드라이브 문자(X:\)로 시작하거나 \\로 시작하는 UNC 경로인지만 본다. }
  Result := (Length(Path) >= 3) and (Path[2] = ':') and (Path[3] = '\');
  if not Result then
    Result := (Length(Path) >= 2) and (Copy(Path, 1, 2) = '\\');
end;

function DirHasFiles(Path: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if not DirExists(Path) then Exit;
  if FindFirst(Path + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

{ (4-1) 전용 상태. Inno Setup Pascal Script는 함수 안에 중첩 프로시저를 못 만들어서
  (컴파일해서 직접 확인함 — "'BEGIN' expected" 오류), 이벤트 핸들러들을 전부 최상위로 빼고
  공유해야 하는 컨트롤·값들도 전역 변수로 둔다. }
var
  DcpForm: TSetupForm;
  DcpPathEdit: TNewPathEdit;
  DcpBrowseButton, DcpUseCustomButton: TNewButton;
  DcpChosenPath: String;
  { TSetupForm에는 코드에서 직접 쓸 수 있는 ModalResult 속성이 없어서(컴파일해서 확인함 —
    "Unknown identifier 'MODALRESULT'"), "사용자 지정 경로에 설치"를 선택했는지는 이 플래그로
    따로 기억한다. }
  DcpCustomPathChosen: Boolean;

procedure DcpUpdateUseCustomEnabled();
begin
  DcpUseCustomButton.Enabled := DcpPathEdit.Enabled and PathLooksValid(DcpPathEdit.Text);
end;

procedure DcpPathEditChange(Sender: TObject);
begin
  DcpUpdateUseCustomEnabled();
end;

procedure DcpCustomNameButtonClick(Sender: TObject);
begin
  DcpPathEdit.Enabled := True;
  DcpBrowseButton.Enabled := True;
  DcpUpdateUseCustomEnabled();
end;

procedure DcpBrowseButtonClick(Sender: TObject);
var
  Dir: String;
begin
  Dir := DcpPathEdit.Text;
  if BrowseForFolder('설치할 폴더를 선택하세요', Dir, True) then
  begin
    DcpPathEdit.Text := Dir;
    DcpUpdateUseCustomEnabled();
  end;
end;

procedure DcpUseCustomButtonClick(Sender: TObject);
begin
  if DirHasFiles(DcpPathEdit.Text) then
  begin
    MsgBox('사용자 지정 경로로 설치할 때에는 빈 폴더로 경로를 정하거나 새 폴더로 정해 주세요.',
           mbError, MB_OK);
    Exit;
  end;
  DcpChosenPath := DcpPathEdit.Text;
  DcpCustomPathChosen := True;
  DcpForm.Close();
end;

{ (4-1): 바탕화면에 이미 내용이 있는 OTP 폴더가 있을 때 뜨는 창.
  <260927_15.1>: "삭제하고 설치"는 폴더를 통째로 지운 뒤 새로 깔았지만, 이제 지우지 않고
  같은 이름의 파일·폴더만 덮어써서 설치한다("덮어쓰기 설치") — 무관한 파일은 그대로 남는다.
  반환값: 0=취소, 1=덮어쓰기 설치, 2=사용자 지정 경로에 설치 — 둘 다 그대로 (5)로 이어진다. }
function ShowDesktopConflictPrompt(): Integer;
var
  MsgLabel: TNewStaticText;
  OverwriteButton, CustomNameButton: TNewButton;
  OverwriteBorderPanel: TPanel;
  NeededHeight, BottomY: Integer;
begin
  Result := 0;
  DcpChosenPath := '';
  DcpCustomPathChosen := False;
  { <260830_3>: 아래쪽 두 줄(버튼 4개)이 실제 캡션 너비로 넓어질 여유를 위해 440→480으로 넓힌다. }
  DcpForm := CreateCustomForm(480, 260, True, True);
  try
    DcpForm.Caption := 'Open Typing Plus';

    { <260830_2-4-3-2>(2): 기존엔 이 문구가 한 줄에 다 안 들어가 자동 줄바꿈됐는데, 하드코딩한
      Height=40이 그 실제 줄 수를 못 따라가 마지막 줄이 가려져 잘렸다(실제 화면에서 확인됨).
      요청대로 두 줄을 직접 나눠 넣고, 실측 폭·높이로 라벨 크기를 정한다. }
    MsgLabel := TNewStaticText.Create(DcpForm);
    MsgLabel.Parent := DcpForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '바탕화면에 이미 "OTP" 폴더가 있습니다.' + #13#10 +
      '덮어쓰기 설치할까요, 다른 이름의 폴더에 설치할까요?';
    EnsureFormWideEnoughForLabel(DcpForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := DcpForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    { <260830_2-4-3-2>(2): 버튼과 경로 입력란을 라벨 바로 아래가 아니라 폼 맨 아래로 내린다
      (요청대로). 우선 컨트롤을 다 만들어 실제 크기(특히 DcpPathEdit의 기본 높이)를 안 뒤,
      아래서 위로 — 찾아보기/사용자 지정 경로 버튼 행 → 경로 입력란 → 덮어쓰기/다른 이름 버튼 행 —
      세로 위치를 계산한다. }
    OverwriteButton := TNewButton.Create(DcpForm);
    OverwriteButton.Caption := '덮어쓰기 설치';
    OverwriteButton.Height := WizardForm.CancelButton.Height;
    OverwriteButton.Width := TextButtonWidth(OverwriteButton.Font, OverwriteButton.Caption, 40, 75);
    OverwriteButton.ModalResult := mrYes;
    { <260927_15> 기본 선택(Default+첫 포커스, [Enter]로 바로 눌림). <260927_15.1>: 폴더를
      통째로 지우던 이전과 달리 지금은 덮어쓰기(같은 이름의 파일·폴더만 교체)라 위험이 훨씬
      적지만, 그래도 기본 선택임을 뚜렷이 보이도록 두껍고 진한 테두리를 둘러 눈에 띄게 한다
      (아래 OverwriteBorderPanel). }
    OverwriteButton.Default := True;

    { <260927_15>/<260927_15.1>: TNewButton엔 테두리색을 직접 지정하는 속성이 없어(224행 주석의
      Canvas 부재와 같은 제약), 버튼보다 사방 4px 큰 진한 색 패널을 뒤에 깔고 그 안에 버튼을 4px
      띄워 앉힌다. 패널을 통째로 그 색으로 채우면(테두리·안쪽 홈 없이) 버튼 둘레로 남는 4px 띠가
      두껍고 진한 테두리처럼 보인다.
      <260927_15.1>: 검정으로는 화면에서 테두리가 드러나지 않았다 — TPanel이 기본으로
      ParentBackground(테마의 부모 배경을 그대로 씀)를 켜 둔 채라 Color 지정이 무시되고 있었다.
      ParentBackground를 꺼야 Color가 실제로 칠해진다. 색도 진한 빨강으로 바꾼다. }
    OverwriteBorderPanel := TPanel.Create(DcpForm);
    OverwriteBorderPanel.Parent := DcpForm;
    OverwriteBorderPanel.ParentBackground := False;
    OverwriteBorderPanel.BevelOuter := bvNone;
    OverwriteBorderPanel.BevelInner := bvNone;
    { TColor는 $00BBGGRR 순서라(RGB 함수는 Pascal Script에 없음 — 컴파일해서 확인함), RGB(139,0,0)
      "다크레드"를 그 순서로 직접 적는다. }
    OverwriteBorderPanel.Color := $0000008B; { 매우 진한 빨강(다크레드, RGB 139,0,0) }
    OverwriteBorderPanel.Width := OverwriteButton.Width + 8;
    OverwriteBorderPanel.Height := OverwriteButton.Height + 8;
    OverwriteBorderPanel.Left := 16;
    OverwriteButton.Parent := OverwriteBorderPanel;
    OverwriteButton.Left := 4;
    OverwriteButton.Top := 4;

    CustomNameButton := TNewButton.Create(DcpForm);
    CustomNameButton.Parent := DcpForm;
    CustomNameButton.Caption := '다른 이름의 폴더에 설치';
    CustomNameButton.Height := WizardForm.CancelButton.Height;
    CustomNameButton.Width := TextButtonWidth(CustomNameButton.Font, CustomNameButton.Caption, 40, 75);
    CustomNameButton.Left := OverwriteBorderPanel.Left + OverwriteBorderPanel.Width + 8;
    CustomNameButton.OnClick := @DcpCustomNameButtonClick;

    DcpPathEdit := TNewPathEdit.Create(DcpForm);
    DcpPathEdit.Parent := DcpForm;
    DcpPathEdit.Left := 16;
    DcpPathEdit.Width := DcpForm.ClientWidth - 32;
    DcpPathEdit.Text := ExpandConstant('{userdesktop}\OTP');
    DcpPathEdit.Enabled := False;
    DcpPathEdit.OnChange := @DcpPathEditChange;

    { <260830_2-4-3>(2): "찾아보기"는 캡션이 짧아 기본 여백만 주면 옹색해 보이므로 좌우 여백(HPadding)을
      더 크게 준다. Left는 아래에서 DcpUseCustomButton의 실제 너비를 안 뒤에 그 왼쪽에 이어 붙인다. }
    DcpBrowseButton := TNewButton.Create(DcpForm);
    DcpBrowseButton.Parent := DcpForm;
    DcpBrowseButton.Caption := '찾아보기';
    DcpBrowseButton.Height := WizardForm.CancelButton.Height;
    DcpBrowseButton.Width := TextButtonWidth(DcpBrowseButton.Font, DcpBrowseButton.Caption, 72, 75);
    DcpBrowseButton.Enabled := False;
    DcpBrowseButton.OnClick := @DcpBrowseButtonClick;

    DcpUseCustomButton := TNewButton.Create(DcpForm);
    DcpUseCustomButton.Parent := DcpForm;
    DcpUseCustomButton.Caption := '사용자 지정 경로에 설치';
    DcpUseCustomButton.Height := WizardForm.CancelButton.Height;
    DcpUseCustomButton.Width := TextButtonWidth(DcpUseCustomButton.Font, DcpUseCustomButton.Caption, 40, 75);
    DcpUseCustomButton.Left := DcpForm.ClientWidth - 16 - DcpUseCustomButton.Width;
    DcpUseCustomButton.Enabled := False;
    DcpUseCustomButton.OnClick := @DcpUseCustomButtonClick;

    { 실제 너비를 다 안 뒤에 왼쪽으로 이어 붙인다(하드코딩된 간격 대신). }
    DcpBrowseButton.Left := DcpUseCustomButton.Left - 8 - DcpBrowseButton.Width;

    { 라벨 실측 높이를 포함해 실제로 필요한 폼 높이를 구하고, 기존 260보다 작으면 늘린다. }
    NeededHeight := 16 + MsgLabel.Height + 16 + OverwriteBorderPanel.Height + 16 +
      DcpPathEdit.Height + 10 + DcpBrowseButton.Height + 10;
    if NeededHeight > DcpForm.ClientHeight then
      DcpForm.ClientHeight := NeededHeight;

    { 폼 맨 아래에서부터 위로 배치 — 찾아보기/사용자 지정 경로 버튼 행 → 경로 입력란 →
      덮어쓰기/다른 이름 버튼 행. }
    BottomY := DcpForm.ClientHeight - 10;
    DcpBrowseButton.Top := BottomY - DcpBrowseButton.Height;
    DcpUseCustomButton.Top := DcpBrowseButton.Top;
    DcpPathEdit.Top := DcpBrowseButton.Top - 10 - DcpPathEdit.Height;
    OverwriteBorderPanel.Top := DcpPathEdit.Top - 16 - OverwriteBorderPanel.Height;
    { 테두리가 없는 CustomNameButton은 두꺼워진 OverwriteBorderPanel과 같은 '행'으로 보이게 세로 가운데를 맞춘다. }
    CustomNameButton.Top := OverwriteBorderPanel.Top + (OverwriteBorderPanel.Height - CustomNameButton.Height) div 2;

    DcpForm.Left := WizardForm.Left + (WizardForm.Width - DcpForm.Width) div 2;
    DcpForm.Top := WizardForm.Top + (WizardForm.Height - DcpForm.Height) div 2;
    DcpForm.ActiveControl := OverwriteButton; { <260927_15> 기본 선택 — 위 테두리로 뚜렷이 표시 }

    if DcpForm.ShowModal() = mrYes then
      Result := 1 { 덮어쓰기 설치 }
    else if DcpCustomPathChosen then
    begin
      FinalInstallDir := DcpChosenPath;
      Result := 2; { 사용자 지정 경로에 설치 }
    end
    else
      Result := 0; { X로 닫음 등 - 취소 }
  finally
    DcpForm.Free();
  end;
end;

{ ============== (4-1-1)/(4-1-2): words.json 비교 후 계속 사용(유지) 여부 ============== }
#include "words_decision.iss"

{ <261003_1>(7) 판단(DecideWordsJson, words_decision.iss)에 넘길 세 파일을 읽는다. 파일이 없거나 읽지 못하면
  지킬 것이 없는 것으로 보고 0(묻지 않고 새 목록 설치). }
function WordsJsonDecision(): Integer;
var
  LiveContent, PackagedContent, OriginalContent: AnsiString;
  LivePath, PackagedPath, OriginalPath: String;
  HasOriginal: Boolean;
begin
  Result := 0;
  LivePath := RoamingOtpFolder() + '\wordslist\words.json';
  PackagedPath := ExpandConstant('{tmp}\words.json'); { 이번 설치 패키지 자체의 기본값 }
  if not FileExists(LivePath) or not FileExists(PackagedPath) then Exit;
  if not LoadStringFromFile(LivePath, LiveContent) then Exit;
  if not LoadStringFromFile(PackagedPath, PackagedContent) then Exit;
  OriginalPath := LocalOtpFolder() + '\wordslist\words-original.json';
  HasOriginal := FileExists(OriginalPath) and LoadStringFromFile(OriginalPath, OriginalContent);
  Result := DecideWordsJson(LiveContent, PackagedContent, OriginalContent, HasOriginal);
end;

{ (4-1-2)·<261003_1.1>(7.3) 사용자가 고쳤고 단계 개정판 번호가 같을 때, 기존의 단어 목록을 계속 사용(유지)할지
  묻는 창. 반환: 1 = 기존의 단어 목록 계속 사용(유지), 2 = 설치 파일 안의 새 단어 목록 사용, 0 = 설치 취소.
  기본 버튼은 '새 단어 목록 사용'이고(<261003_1.1> — 고친 목록을 잃는 것은 사용자 책임), 창 닫기(X)·[Esc]는
  설치 취소다(<261003_1.1>(7.3.1)). }
function ShowWordsJsonKeepPrompt(): Integer;
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  NewListButton, KeepButton: TNewButton;
  Bottom: Integer;
begin
  ConfirmForm := CreateCustomForm(520, 170, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '기존 설치된 것 중에서 자리연습·산성비 오락의 단어 목록(OTP 폴더\wordslist\단어 목록 편집(words.json))이 변경되어' + #13#10 +
      '있습니다. 기존의 단어 목록이 설치하려는 단어 목록보다 최신일 수도 있습니다.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    NewListButton := TNewButton.Create(ConfirmForm);
    NewListButton.Parent := ConfirmForm;
    NewListButton.Caption := '설치 파일 안의 새 단어 목록 사용';
    NewListButton.Height := WizardForm.CancelButton.Height;
    NewListButton.Width := TextButtonWidth(NewListButton.Font, NewListButton.Caption, 40, 75);
    NewListButton.ModalResult := mrNo;
    NewListButton.Default := True;

    KeepButton := TNewButton.Create(ConfirmForm);
    KeepButton.Parent := ConfirmForm;
    KeepButton.Caption := '기존의 단어 목록 계속 사용(유지)';
    KeepButton.Height := WizardForm.CancelButton.Height;
    KeepButton.Width := TextButtonWidth(KeepButton.Font, KeepButton.Caption, 40, 75);
    KeepButton.ModalResult := mrYes;

    AddEscCancelButton(ConfirmForm);   { [Esc] = 설치 취소 }

    Bottom := LayoutTwoButtons(ConfirmForm, NewListButton, KeepButton, MsgLabel.Top + MsgLabel.Height + 16);
    ConfirmForm.ClientHeight := Bottom + 10;
    ConfirmForm.ActiveControl := NewListButton;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    case ConfirmForm.ShowModal() of
      mrYes: Result := 1;
      mrNo: Result := 2;
    else
      Result := 0;   { 창 닫기(X)·[Esc] }
    end;
  finally
    ConfirmForm.Free();
  end;
end;

function ShouldInstallWordsJson(): Boolean;
begin
  Result := not KeepWordsJson;
end;

{ ============== <261005_1> 이미 설치된 것보다 낮은 버전을 설치하려 할 때 경고 ============== }
{ 설치 폴더(FinalInstallDir)에 이미 열린타자+.exe 가 있고, 그 파일 버전이 지금 설치하려는 버전보다 높으면 묻는다.
  반환: True = 계속 설치(경고 없음 포함), False = 설치 취소. 파일이 없거나 버전을 읽을 수 없으면 묻지 않는다. }
function ConfirmDowngrade(): Boolean;
var
  ExePath, Installed, Setup: String;
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  CancelBtn, InstallBtn: TNewButton;
  Bottom: Integer;
begin
  Result := True;
  ExePath := AddBackslash(FinalInstallDir) + '{#MyAppExeName}';
  if not FileExists(ExePath) then Exit;
  if not GetVersionNumbersString(ExePath, Installed) then Exit;
  Setup := '{#MyAppVersion}';
  if CompareVersionStrings(Installed, Setup) <= 0 then Exit;   { 같거나 낮으면 경고하지 않는다 }

  ConfirmForm := CreateCustomForm(480, 170, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '이 폴더에 더 높은 버전의 Open Typing Plus(설치된 버전: ' + Installed + ')가 설치되어 있습니다.' + #13#10 +
      '지금 설치하려는 버전(' + Setup + ')은 더 낮은 버전입니다.' + #13#10 +
      '낮은 버전을 설치하면 일부 기능이나 단어 목록이 예전 것으로 돌아갑니다.' + #13#10 +
      '낮은 버전을 설치하시겠습니까?';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    CancelBtn := TNewButton.Create(ConfirmForm);
    CancelBtn.Parent := ConfirmForm;
    CancelBtn.Caption := '설치 취소';
    CancelBtn.Height := WizardForm.CancelButton.Height;
    CancelBtn.Width := TextButtonWidth(CancelBtn.Font, CancelBtn.Caption, 40, 75);
    CancelBtn.ModalResult := mrCancel;
    CancelBtn.Default := True;
    CancelBtn.Cancel := True;   { 창 닫기(X)·[Esc]도 설치 취소 }

    InstallBtn := TNewButton.Create(ConfirmForm);
    InstallBtn.Parent := ConfirmForm;
    InstallBtn.Caption := '낮은 버전 설치하기';
    InstallBtn.Height := WizardForm.CancelButton.Height;
    InstallBtn.Width := TextButtonWidth(InstallBtn.Font, InstallBtn.Caption, 40, 75);
    InstallBtn.ModalResult := mrYes;

    Bottom := LayoutTwoButtons(ConfirmForm, CancelBtn, InstallBtn, MsgLabel.Top + MsgLabel.Height + 16);
    ConfirmForm.ClientHeight := Bottom + 10;
    ConfirmForm.ActiveControl := CancelBtn;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    Result := (ConfirmForm.ShowModal() = mrYes);
  finally
    ConfirmForm.Free();
  end;
end;

{ ============== 이벤트 함수 ============== }
{ <260830_2-2> 자동 생성된 삭제 프로그램(unins000.exe / .dat)의 이름을 "설치삭제.exe"로
  바꾼다. 두 파일을 같이 바꿔야 한다 — 삭제 프로그램은 자기 이름과 같은 이름의 .dat에서
  삭제할 파일 목록을 읽으므로, 이름이 서로 안 맞으면 못 찾는다. 제일 마지막 단계(ssDone)에서
  해야 그 시점엔 두 파일이 확실히 다 만들어져 있다.
  같은 폴더에 다시 설치하면 첫 설치의 설치삭제.exe/.dat 가 이미 있어 RenameFile 이 실패하고(대상 이름의 파일이
  있으면 실패 — Inno 도움말), 새 unins000.* 와 예전 설치삭제.* 가 함께 남았다(2026-10-05 실제 확인). 그래서
  ssInstall 에서 예전 설치삭제.* 를 unins000.* 로 되돌려 Inno 가 그 기록에 이어 쓰게 하고
  (PrepareUninstallerForAppend), 여기서는 그래도 남은 예전 설치삭제.* 를 지운 뒤 이름을 바꾼다. }

{ 삭제 프로그램 두 파일을 한 쌍으로 개명한다. 하나만 바뀌면 이름이 어긋나 삭제 프로그램이 자기 .dat 를 못
  찾으므로, .dat 개명이 실패하면 .exe 를 원래 이름으로 되돌린다. 대상 이름의 파일은 없어야 한다. }
function RenameUninstallerPair(OldExe, OldDat, NewExe, NewDat: String): Boolean;
begin
  Result := False;
  if not FileExists(OldDat) then
    Exit;
  if FileExists(OldExe) and not RenameFile(OldExe, NewExe) then
    Exit;
  if RenameFile(OldDat, NewDat) then
    Result := True
  else if FileExists(NewExe) then
    RenameFile(NewExe, OldExe);
end;

// Inno 는 같은 폴더(UninstallFilesDir)에서 AppId 가 같은 unins???.dat 를 찾아 그 삭제 기록에 이어 쓴다(Inno 도움말
// "Uninstall log appending"). 우리가 설치삭제.dat 로 이름을 바꿔 두어 Inno 가 그걸 못 찾고 늘 새 기록을 만들었으므로,
// 실제 설치가 시작되기 직전(ssInstall)에 unins000.* 로 되돌려 놓는다. 그러면 삭제할 때 예전 설치에서 넣은 파일도
// 함께 지워진다. 이미 unins000.* 가 있으면(이 고침 전의 설치 파일로 다시 설치해 둘 다 남은 경우) 건드리지 않는다 —
// Inno 가 그 unins000.dat 에 이어 쓰고, 남은 예전 설치삭제.* 는 ssDone 에서 지운다.
// (중괄호 경로 상수를 쓰므로 // 줄 주석으로 쓴다.)
procedure PrepareUninstallerForAppend();
var
  Dir: String;
begin
  Dir := ExpandConstant('{app}\설치삭제');
  if FileExists(Dir + '\설치삭제.dat') and not FileExists(Dir + '\unins000.dat') and
     not FileExists(Dir + '\unins000.exe') then
    UninstallerRestoredForAppend := RenameUninstallerPair(
      Dir + '\설치삭제.exe', Dir + '\설치삭제.dat', Dir + '\unins000.exe', Dir + '\unins000.dat');
end;

procedure RenameUninstaller();
var
  OldExe, OldDat, NewExe, NewDat: String;
begin
  // <260831_1>: [Setup]의 UninstallFilesDir 지정으로 unins000.exe/.dat는 이제 OTP 폴더
  // 바로 아래가 아니라 그 안의 "설치삭제" 하위 폴더에 생성된다. 삭제 프로그램 경로 상수가
  // 그 실제 위치를 반영하므로(ExpandConstant가 런타임에 실제 경로를 돌려줌 — 폴더 경로를
  // 직접 조립하지 않고 그대로 믿는다), 개명 결과도 같은 폴더 안에 남긴다.
  // (참고: { } 블록 주석 안에 저 경로 상수를 그대로 적으면 중괄호가 주석을 조기에 닫아 버려
  // 컴파일 에러가 난다 — 실제로 겪음. 그래서 이 설명은 // 줄 주석으로 쓴다.)
  OldExe := ExpandConstant('{uninstallexe}');
  OldDat := ChangeFileExt(OldExe, '.dat');
  NewExe := ExpandConstant('{app}\설치삭제\설치삭제.exe');
  NewDat := ExpandConstant('{app}\설치삭제\설치삭제.dat');
  UninstallerNameDone := True;
  if not FileExists(OldDat) then
    Exit;
  // 이번 설치가 이어 쓰지 않은 예전 설치삭제.exe/.dat 가 남아 있으면 지운다(남겨 두면 사용자가 낡은 삭제
  // 프로그램을 실행하게 된다). 이어 쓴 경우엔 ssInstall 에서 이미 unins000.* 로 이름이 바뀌어 여기 없다.
  if FileExists(NewExe) then
    DeleteFile(NewExe);
  if FileExists(NewDat) then
    DeleteFile(NewDat);
  if FileExists(NewExe) or FileExists(NewDat) or
     not RenameUninstallerPair(OldExe, OldDat, NewExe, NewDat) then
    MsgBox('삭제 프로그램의 이름을 "설치삭제.exe"로 바꾸지 못했습니다.' + #13#10 +
           '프로그램을 지울 때는 아래 폴더의 "' + ExtractFileName(OldExe) + '"을 실행해 주세요.' + #13#10 +
           ExtractFileDir(OldExe), mbError, MB_OK);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  { 라이선스 페이지에서 "다음"을 눌러 넘어가는 시점 = 라이선스에 동의한 시점
    (Inno Setup이 "동의하지 않음" 상태에서는 애초에 다음으로 못 넘어가게 막아준다).
    DisableDirPage=yes라 별도의 "대상 위치 선택" 페이지가 없으므로 설치 전 질문을 전부 여기서 처리한다.
    <261005_1>(7): 낮은 버전 설치 경고가 다른 질문보다 먼저 뜨도록, 설치 폴더를 정하는 (4) 바탕화면 OTP 폴더
    확인을 맨 앞에 두고 → 낮은 버전 경고 → (3) AppData 신원 확인 → (4-1-1) 단어 목록 질문 순서로 한다.
    (예전 순서는 단어 목록 → (3) → (4)였다. 앞의 질문들은 선택만 기억하고 실제 삭제·복사는 설치가 확정된 뒤에
    하므로 순서를 바꿔도 동작은 같다.) }
  if CurPageID = wpLicense then
  begin
    InstallCancelled := False;
    SilentCancel := False;
    FinalInstallDir := ExpandConstant('{autodesktop}\OTP');
    KeepWordsJson := False;
    KeepChosen := False;
    PendingRoamingCleanup := False;
    PendingLocalCleanup := False;
    RoamingCleanupChosen := False;
    PreservedBackupDir := '';
    PreservedRestoreOk := False;
    StageVersionMismatch := False;
    StageMismatchBackupDir := '';
    // (4-1-1) 비교, words-original.json 생성 어느 쪽이든 "이번 설치 패키지 자체의 기본
    // words.json 내용"이 필요하므로, 여기서 미리(조건 없이) tmp 폴더에 꺼내 둔다.
    ExtractTemporaryFile('words.json');

    if DesktopOtpHasFiles() then
    begin
      case ShowDesktopConflictPrompt() of
        0: begin { 취소 } Result := False; WizardForm.Close(); Exit; end;
        1: begin { 덮어쓰기 설치 — [Files]가 같은 이름의 파일만 덮어쓰므로 여기선 할 일이 없다 } end;
        2: begin { 사용자 지정 경로에 설치 - FinalInstallDir는 ShowDesktopConflictPrompt 안에서 이미 설정됨 } end;
      end;
    end;

    { <261005_1> 설치 폴더가 정해진 뒤, 그 폴더의 열린타자+.exe 가 더 높은 버전이면 경고한다. }
    if not ConfirmDowngrade() then
    begin
      SilentCancel := True;   { 사용자가 이미 "설치 취소"를 골랐다 — 다시 묻지 않는다 }
      Result := False;
      WizardForm.Close();
      Exit;
    end;

    CheckAppDataIdentity();
    if InstallCancelled then
    begin
      { Result := False 를 반드시 함께 준다. WizardForm.Close()는 즉시 종료가 아니라 표준
        "설치를 종료하시겠습니까?" 확인창을 띄우는데, 거기서 사용자가 "아니요"를 고르면 제어가
        여기로 돌아온다 — 그때 Result가 True면 취소했던 설치가 그대로 진행돼 버린다. }
      Result := False;
      WizardForm.Close();
      Exit;
    end;
    RoamingCleanupChosen := PendingRoamingCleanup;

    { <260831 검토>(4-1-1)/(4-1-2): 이 비교를 어떤 삭제보다도 먼저 한다(삭제는 설치가 확정된 뒤에 한다).
      예전엔 (3-1)의 DelTree가 라이브 words.json을 먼저 지운 뒤에야 비교가 돌아서,
      FileExists가 항상 False → "달라진 것 없음"으로 판정 → 계속 사용 여부 창이 정상 재설치
      경로에서는 아예 뜨지 않았고 사용자가 고친 단어 목록이 매번 사라졌다. }
    case WordsJsonDecision() of
      1: case ShowWordsJsonKeepPrompt() of
           1: KeepChosen := True;
           2: KeepChosen := False;
         else
           begin
             { <261003_1.1>(7.3.1) 창 닫기(X)·[Esc] = 설치 취소 }
             MsgBox(CancelMessage, mbInformation, MB_OK);
             SilentCancel := True;
             Result := False;
             WizardForm.Close();
             Exit;
           end;
         end;
      2: { <261003_1>(7.5): 단계 구성이 바뀌어 고친 목록을 그대로 쓸 수 없다 — '계속 사용(유지)'를 주지 않는다.
           사본을 만들고 안내하는 일은 [설치]를 누른 직후(PrepareToInstall)에 한다(<261003_1.1>(7.5.1.6)). }
         StageVersionMismatch := True;
    end;
    KeepWordsJson := KeepChosen;

    WizardForm.DirEdit.Text := FinalInstallDir;
  end;
end;

{ <261003_1.1>(7.3.1)·<261005_1>: 사용자가 이미 취소를 고른 뒤 WizardForm.Close()로 끝낼 때는 Inno 기본
  "설치를 종료하시겠습니까?"를 다시 묻지 않는다. 그 밖의 경우(사용자가 설치 창의 [취소]·X를 누름, 예전부터
  있던 (3)(4) 창의 취소)는 지금처럼 묻는다. }
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if SilentCancel then
    Confirm := False;
end;

{ <260901_2>: 완료 페이지의 "Open Typing Plus 실행" 체크박스를 직접 만든다(왜 Inno 자동 생성
  체크박스를 안 쓰는지는 [Run] 섹션의 <260901_2> 주석 참고). }
procedure LaunchCheckBoxClick(Sender: TObject);
begin
  LaunchAfterFinish := LaunchCheckBox.Checked;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and (LaunchCheckBox = nil) then
  begin
    LaunchCheckBox := TNewCheckBox.Create(WizardForm);
    LaunchCheckBox.Parent := WizardForm.FinishedPage;
    LaunchCheckBox.Caption := ExpandConstant('{cm:LaunchProgram,Open Typing Plus}');
    LaunchCheckBox.Left := WizardForm.FinishedLabel.Left;
    LaunchCheckBox.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + ScaleY(16);
    LaunchCheckBox.Width := WizardForm.FinishedPage.ClientWidth - LaunchCheckBox.Left;
    LaunchCheckBox.Height := ScaleY(17);
    LaunchCheckBox.Checked := True; { 기본 선택됨 — <260830_2-4>(6)과 동일한 동작 }
    LaunchCheckBox.OnClick := @LaunchCheckBoxClick;
    LaunchAfterFinish := True;
  end;
end;

{ ============== <261003_1.1>(7.3.2)(7.5.1) 바탕화면 사본 폴더 ============== }

{ 실제 설치가 시작되기 전에 설치가 취소되면, 이번 설치에서 바탕화면에 만든 사본 폴더(임시 폴더 포함)를 지운다
  (<261003_1.1>(7.3.2.3)(7.5.1.6.1) — 이때 AppData 의 원래 단어 목록은 그대로 있으므로 사본이 필요 없다).
  두 변수는 이번 설치에서 만든 폴더일 때만 값이 있으므로, 예전부터 있던 폴더를 지우지 않는다. }
procedure RemoveDesktopCopiesBeforeInstall();
begin
  if (PreservedBackupDir <> '') and DirExists(PreservedBackupDir) then
    DelTree(PreservedBackupDir, True, True, True);
  PreservedBackupDir := '';
  if (StageMismatchBackupDir <> '') and DirExists(StageMismatchBackupDir) then
    DelTree(StageMismatchBackupDir, True, True, True);
  StageMismatchBackupDir := '';
end;

{ <261003_1.1>(7.3.2.1)(7.3.2.1.1) 임시 폴더(PreservedBackupDir)를 남길 때의 안내. FirstLine 이 첫 줄.
  AppData 에 words.json 이 없으면 고친 목록을 다시 쓰는 법(복사해 넣을 곳)을 알리고, 있으면(프로그램이 고친 목록을
  그대로 쓰므로) 임시 폴더를 지워도 된다고 알린다. 경로는 이 PC 의 실제 경로로 보인다. }
function KeptTempCopyMessage(const FirstLine: String): String;
begin
  if FileExists(RoamingOtpFolder() + '\wordslist\words.json') then
    Result := FirstLine + #13#10 + #13#10 +
              '사본은 아래 폴더에 남겨져 있으며 삭제해도 됩니다:' + #13#10 + PreservedBackupDir
  else
    Result := FirstLine + #13#10 + #13#10 +
              '사본을 아래 폴더에 그대로 남겨 두었습니다:' + #13#10 + PreservedBackupDir + #13#10 + #13#10 +
              '이 상태로는 Open Typing Plus가 고치신 단어 목록을 쓰지 못합니다.' + #13#10 +
              '위 폴더의 words.json을 아래 폴더에 복사해 넣어 주세요.' + #13#10 +
              '만일 아래 경로가 없다면 폴더를 만들어 넣으세요:' + #13#10 + RoamingOtpFolder() + '\wordslist';
end;

{ <261003_1.1>(7.5.1) (7.5)에서 사본을 만들지 못했을 때의 선택창.
  반환: 1 = 설치 파일 안의 새 단어 목록 설치하기(기본), 2 = 고친 기존의 단어 목록 계속 사용하기, 0 = 설치 취소(X·[Esc]). }
function ShowStageCopyFailedPrompt(const AttemptedDir: String): Integer;
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  NewListButton, KeepButton: TNewButton;
  Bottom: Integer;
begin
  ConfirmForm := CreateCustomForm(520, 170, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '자리연습 단계 구성이 바뀌어 새 단어 목록을 설치해야 하지만,' + #13#10 +
      '고치신 기존의 단어 목록 사본(보존용)을 바탕화면에 만들지 못했습니다:' + #13#10 +
      AttemptedDir + #13#10 + #13#10 +
      '새 단어 목록을 설치하면 고치신 기존의 단어 목록은 사본 없이 사라집니다.' + #13#10 +
      '고치신 기존의 단어 목록을 계속 쓰면, 바뀐 자리연습 단계와 맞지 않아 일부 단어가 쓰이지 않거나' + #13#10 +
      '단어 연습이 짧아질 수 있습니다.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    NewListButton := TNewButton.Create(ConfirmForm);
    NewListButton.Parent := ConfirmForm;
    NewListButton.Caption := '설치 파일 안의 새 단어 목록 설치하기';
    NewListButton.Height := WizardForm.CancelButton.Height;
    NewListButton.Width := TextButtonWidth(NewListButton.Font, NewListButton.Caption, 40, 75);
    NewListButton.ModalResult := mrNo;
    NewListButton.Default := True;

    KeepButton := TNewButton.Create(ConfirmForm);
    KeepButton.Parent := ConfirmForm;
    KeepButton.Caption := '고친 기존의 단어 목록 계속 사용하기';
    KeepButton.Height := WizardForm.CancelButton.Height;
    KeepButton.Width := TextButtonWidth(KeepButton.Font, KeepButton.Caption, 40, 75);
    KeepButton.ModalResult := mrYes;

    AddEscCancelButton(ConfirmForm);   { [Esc] = 설치 취소 }

    Bottom := LayoutTwoButtons(ConfirmForm, NewListButton, KeepButton, MsgLabel.Top + MsgLabel.Height + 16);
    ConfirmForm.ClientHeight := Bottom + 10;
    ConfirmForm.ActiveControl := NewListButton;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    case ConfirmForm.ShowModal() of
      mrNo: Result := 1;
      mrYes: Result := 2;
    else
      Result := 0;
    end;
  finally
    ConfirmForm.Free();
  end;
end;

{ ============== <261005_2> 같은 폴더에 다시 설치할 때 프로그램 폴더의 남는 파일 정리 ============== }

{ 2026-10-06 /qksqhr: 정션·심볼릭 링크(재분석 지점)는 따라 들어가지 않는다 — 따라가면 링크가 가리키는 설치 폴더 밖의
  파일까지 '남는 파일'로 지우게 되고, 서로 가리키는 링크면 끝없이 돈다. }
const
  FILE_ATTRIBUTE_READONLY_ = $1;
  FILE_ATTRIBUTE_REPARSE_POINT_ = $400;
  INVALID_FILE_ATTRIBUTES_ = $FFFFFFFF;
  MaxListedFiles = 20;

function GetFileAttributesW(lpFileName: String): Cardinal; external 'GetFileAttributesW@kernel32.dll stdcall';
function SetFileAttributesW(lpFileName: String; dwFileAttributes: Cardinal): Boolean; external 'SetFileAttributesW@kernel32.dll stdcall';

function IsReparsePoint(const Path: String): Boolean;
var
  A: Cardinal;
begin
  A := GetFileAttributesW(Path);
  Result := (A <> INVALID_FILE_ATTRIBUTES_) and ((A and FILE_ATTRIBUTE_REPARSE_POINT_) <> 0);
end;

{ 읽기 전용이면 그 속성을 걷어 낸다(지우기 직전·지울 수 있는지 볼 때). 원래 속성을 돌려준다(없으면 INVALID). }
function ClearReadOnly(const Path: String): Cardinal;
begin
  Result := GetFileAttributesW(Path);
  if (Result <> INVALID_FILE_ATTRIBUTES_) and ((Result and FILE_ATTRIBUTE_READONLY_) <> 0) then
    SetFileAttributesW(Path, Result and not FILE_ATTRIBUTE_READONLY_);
end;

{ 파일 목록 문자열(앞에 줄바꿈을 붙여 이어 쓴 것)이 너무 길면 앞의 MaxListedFiles 개만 보이고 나머지는 개수로 줄인다. }
function ListForDisplay(List: TStringList): String;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to List.Count - 1 do
    if I < MaxListedFiles then
      Result := Result + #13#10 + List[I];
  if List.Count > MaxListedFiles then
    Result := Result + #13#10 + '… 외 ' + IntToStr(List.Count - MaxListedFiles) + '개';
end;

{ BaseDir\Rel 아래(하위 폴더 포함)의 파일 중 이번 설치 파일 목록(InstallManifest)에 없는 것을, 설치 폴더 기준
  상대 경로(예: stages\abc.json)로 List 에 더한다. }
procedure CollectLeftovers(const BaseDir, Rel: String; List: TStringList);
var
  FindRec: TFindRec;
  Child: String;
begin
  if FindFirst(AddBackslash(BaseDir) + Rel + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Child := Rel + '\' + FindRec.Name;
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          begin
            if (FindRec.Attributes and FILE_ATTRIBUTE_REPARSE_POINT_) = 0 then   { 링크된 폴더는 건너뛴다 }
              CollectLeftovers(BaseDir, Child, List);
          end
          else if ((FindRec.Attributes and FILE_ATTRIBUTE_REPARSE_POINT_) = 0) and
                  not ManifestHas(InstallManifest, Child) then   { 파일 링크도 건드리지 않는다(가리키는 파일이 밖에 있을 수 있다) }
            List.Add(Child);
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

{ 설치 폴더(FinalInstallDir)의 stages·layouts·hands 에서 남는 파일 목록을 새로 만든다. 부르는 쪽이 Free 한다. }
function FindLeftovers(): TStringList;
begin
  Result := TStringList.Create;
  if FinalInstallDir = '' then Exit;   { 설치 폴더가 정해지지 않았으면 현재 폴더를 훑지 않도록 아무것도 찾지 않는다 }
  { 세 폴더 자체가 링크(정션)면 손대지 않는다. }
  if DirExists(AddBackslash(FinalInstallDir) + 'stages') and not IsReparsePoint(AddBackslash(FinalInstallDir) + 'stages') then
    CollectLeftovers(FinalInstallDir, 'stages', Result);
  if DirExists(AddBackslash(FinalInstallDir) + 'layouts') and not IsReparsePoint(AddBackslash(FinalInstallDir) + 'layouts') then
    CollectLeftovers(FinalInstallDir, 'layouts', Result);
  if DirExists(AddBackslash(FinalInstallDir) + 'hands') and not IsReparsePoint(AddBackslash(FinalInstallDir) + 'hands') then
    CollectLeftovers(FinalInstallDir, 'hands', Result);
end;

{ <261005_2>(7) 지울 수 있는 상태인가 — 사용 중이 아니고 읽기 전용 등으로 막혀 있지 않은가. 파일을 바꾸지 않고
  쓰기 권한·독점으로 잠깐 열어 본다. }
function CanDeleteFile(const Path: String): Boolean;
var
  S: TFileStream;
  Attr: Cardinal;
begin
  Result := False;
  { 읽기 전용 파일은 쓰기로 열리지 않아 늘 '지울 수 없음'이 되므로, 잠깐 속성을 걷어 내고 본 뒤 되돌린다
    (실제로 지울 때도 걷어 낸다 — HandleLeftovers). }
  Attr := ClearReadOnly(Path);
  try
    S := TFileStream.Create(Path, fmOpenReadWrite or fmShareExclusive);
    S.Free;
    Result := True;
  except
    Result := False;
  end;
  if (Attr <> INVALID_FILE_ATTRIBUTES_) and ((Attr and FILE_ATTRIBUTE_READONLY_) <> 0) then
    SetFileAttributesW(Path, Attr);
end;

{ <261005_2>(4) Dir 아래 하위 폴더 중 비어 있는 것을 지운다(안쪽부터). Dir 자체는 지우지 않는다. }
procedure RemoveEmptySubdirs(const Dir: String);
var
  FindRec: TFindRec;
  Sub: String;
begin
  if FindFirst(AddBackslash(Dir) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') and
           ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           ((FindRec.Attributes and FILE_ATTRIBUTE_REPARSE_POINT_) = 0) then
        begin
          Sub := AddBackslash(Dir) + FindRec.Name;
          RemoveEmptySubdirs(Sub);
          RemoveDir(Sub);   { 비어 있을 때만 지워진다 }
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure LeftoverOpenFolderClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', FinalInstallDir, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

{ <261005_2>(3) 남는 파일 안내. 반환: True = 계속 설치(기본), False = 설치 취소(X·[Esc] 포함). }
function ShowLeftoverPrompt(Leftovers: TStringList): Boolean;
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  FileListMemo: TNewMemo;
  OpenButton, ContinueButton, CancelBtn: TNewButton;
  Y, W: Integer;
begin
  ConfirmForm := CreateCustomForm(520, 300, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '설치 폴더의 stages·layouts·hands 폴더에 이번 설치 파일에 없는 파일이 있습니다.' + #13#10 +
      '계속 설치하면 아래 파일들은 삭제됩니다.' + #13#10 +
      '직접 넣으신 파일 중 필요한 것이 있으면, 지금 다른 곳으로 옮긴 뒤 [계속 설치]를 누르세요.' + #13#10 +
      '(같은 이름의 파일은 새 파일로 바뀝니다.)';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    FileListMemo := TNewMemo.Create(ConfirmForm);
    FileListMemo.Parent := ConfirmForm;
    FileListMemo.Left := 16;
    FileListMemo.Top := MsgLabel.Top + MsgLabel.Height + 8;
    FileListMemo.Width := ConfirmForm.ClientWidth - 32;
    FileListMemo.Height := 130;
    FileListMemo.ReadOnly := True;
    FileListMemo.ScrollBars := ssVertical;
    FileListMemo.Lines.Text := Leftovers.Text;

    OpenButton := TNewButton.Create(ConfirmForm);
    OpenButton.Parent := ConfirmForm;
    OpenButton.Caption := '폴더 열기';
    OpenButton.Height := WizardForm.CancelButton.Height;
    OpenButton.Width := TextButtonWidth(OpenButton.Font, OpenButton.Caption, 40, 75);
    OpenButton.OnClick := @LeftoverOpenFolderClick;   { 창을 닫지 않는다 }

    ContinueButton := TNewButton.Create(ConfirmForm);
    ContinueButton.Parent := ConfirmForm;
    ContinueButton.Caption := '계속 설치';
    ContinueButton.Height := WizardForm.CancelButton.Height;
    ContinueButton.Width := TextButtonWidth(ContinueButton.Font, ContinueButton.Caption, 40, 75);
    ContinueButton.ModalResult := mrOk;
    ContinueButton.Default := True;

    CancelBtn := TNewButton.Create(ConfirmForm);
    CancelBtn.Parent := ConfirmForm;
    CancelBtn.Caption := '설치 취소';
    CancelBtn.Height := WizardForm.CancelButton.Height;
    CancelBtn.Width := TextButtonWidth(CancelBtn.Font, CancelBtn.Caption, 40, 75);
    CancelBtn.ModalResult := mrCancel;
    CancelBtn.Cancel := True;   { 창 닫기(X)·[Esc]도 설치 취소 }

    Y := FileListMemo.Top + FileListMemo.Height + 16;
    if OpenButton.Width + 8 + ContinueButton.Width + 8 + CancelBtn.Width <= ConfirmForm.ClientWidth - 32 then
    begin
      { 한 줄: 왼쪽에 폴더 열기, 오른쪽에 계속 설치·설치 취소 }
      OpenButton.Left := 16;
      CancelBtn.Left := ConfirmForm.ClientWidth - 16 - CancelBtn.Width;
      ContinueButton.Left := CancelBtn.Left - 8 - ContinueButton.Width;
      OpenButton.Top := Y;
      ContinueButton.Top := Y;
      CancelBtn.Top := Y;
      ConfirmForm.ClientHeight := Y + CancelBtn.Height + 10;
    end
    else
    begin
      { 창 너비를 넘길 것 같으면 상/하로 }
      W := OpenButton.Width;
      if ContinueButton.Width > W then W := ContinueButton.Width;
      if CancelBtn.Width > W then W := CancelBtn.Width;
      OpenButton.Width := W;
      ContinueButton.Width := W;
      CancelBtn.Width := W;
      OpenButton.Left := ConfirmForm.ClientWidth - 16 - W;
      ContinueButton.Left := OpenButton.Left;
      CancelBtn.Left := OpenButton.Left;
      OpenButton.Top := Y;
      ContinueButton.Top := OpenButton.Top + OpenButton.Height + 6;
      CancelBtn.Top := ContinueButton.Top + ContinueButton.Height + 6;
      ConfirmForm.ClientHeight := CancelBtn.Top + CancelBtn.Height + 10;
    end;
    ConfirmForm.ActiveControl := ContinueButton;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    Result := (ConfirmForm.ShowModal() = mrOk);
  finally
    ConfirmForm.Free();
  end;
end;

{ <261005_2> 남는 파일 확인과 삭제. 반환: '' = 계속 설치, 그 밖 = 설치를 취소하며 보여 줄 문구.
  묻는 것(안내 창)을 먼저 하고, 되돌릴 수 없는 삭제는 이 함수의 맨 끝에서 한다(<261005_2>(6)). }
function HandleLeftovers(): String;
var
  Leftovers, Undeletable, Failed: TStringList;
  I: Integer;
  P: String;
  Attr: Cardinal;
begin
  Result := '';
  Leftovers := FindLeftovers();
  try
    if Leftovers.Count = 0 then Exit;   { (8) 남는 파일이 없으면 안내 없이 설치 }
    if not ShowLeftoverPrompt(Leftovers) then
    begin
      Result := CancelMessage;   { (5) }
      Exit;
    end;
  finally
    Leftovers.Free;
  end;

  { (4) "계속 설치"를 누른 그 순간의 남는 파일을 다시 확인한다(그 사이 사용자가 옮긴 파일은 이미 없다). }
  Leftovers := FindLeftovers();
  try
    { (7)(7.1) 먼저 모두 지울 수 있는지 확인하고, 하나라도 지울 수 없으면 아무것도 지우지 않고 취소한다. }
    Undeletable := TStringList.Create;
    try
      for I := 0 to Leftovers.Count - 1 do
        if not CanDeleteFile(AddBackslash(FinalInstallDir) + Leftovers[I]) then
          Undeletable.Add(Leftovers[I]);
      if Undeletable.Count > 0 then
      begin
        { 줄 맨 앞이 #13 이면 전처리기가 지시어로 읽으므로 한 줄에 둔다. 목록이 길면 앞의 몇 개만 보인다. }
        Result := '일부 파일을 지울 수 없어 설치를 취소합니다. Open Typing Plus가 실행 중이면 끄고 다시 설치해 주세요.' + #13#10 + ListForDisplay(Undeletable);
        Exit;
      end;
    finally
      Undeletable.Free;
    end;
    { (7.2) 모두 지울 수 있으면 지운다. 도중에 실패한 파일이 있어도 설치는 계속하고, 끝나면 알린다. }
    Failed := TStringList.Create;
    try
      for I := 0 to Leftovers.Count - 1 do
      begin
        P := AddBackslash(FinalInstallDir) + Leftovers[I];
        Attr := ClearReadOnly(P);
        { 그사이 이미 없어진 파일은 실패로 치지 않는다. 지우지 못했으면 읽기 전용 속성을 되돌린다. }
        if not DeleteFile(P) and FileExists(P) then
        begin
          if (Attr <> INVALID_FILE_ATTRIBUTES_) and ((Attr and FILE_ATTRIBUTE_READONLY_) <> 0) then
            SetFileAttributesW(P, Attr);
          Failed.Add(Leftovers[I]);
        end;
      end;
      LeftoverDeleteFailed := ListForDisplay(Failed);
    finally
      Failed.Free;
    end;
  finally
    Leftovers.Free;
  end;
  { (4) 비게 된 하위 폴더도 지운다(세 폴더 자체는 지우지 않는다). }
  if DirExists(AddBackslash(FinalInstallDir) + 'stages') then RemoveEmptySubdirs(AddBackslash(FinalInstallDir) + 'stages');
  if DirExists(AddBackslash(FinalInstallDir) + 'layouts') then RemoveEmptySubdirs(AddBackslash(FinalInstallDir) + 'layouts');
  if DirExists(AddBackslash(FinalInstallDir) + 'hands') then RemoveEmptySubdirs(AddBackslash(FinalInstallDir) + 'hands');
end;

{ ============== 준비 완료 화면에서 [설치]를 누른 직후 — 실제 설치(파일 복사·AppData 정리) 전 ============== }
{ <261003_1.1>(7.5.1.6)·<261005_2>(6): 단어 목록 사본 만들기와 그에 따른 선택, 남는 파일 안내를 여기서 한다.
  사용자에게 묻는 것과 사본 만들기를 모두 먼저 하고, 되돌릴 수 없는 남는 파일 삭제는 맨 마지막에 한다.
  빈 문자열이 아닌 값을 돌려주면 Inno 가 그 문구를 보여 주고 설치를 시작하지 않는다. 그때는 이번에 만든 바탕화면
  사본 폴더를 지운다(실제 설치 전 취소 — AppData 의 원래 단어 목록은 그대로 있다). }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  LivePath, Attempted, FirstLine, Stamp: String;
  LiveContent, PackagedContent: AnsiString;
begin
  Result := '';
  { 이 단계가 다시 불려도(뒤로 갔다 다시 [설치]) 앞 화면에서 고른 값에서 다시 시작한다. }
  RemoveDesktopCopiesBeforeInstall();
  KeepWordsJson := KeepChosen;
  PendingRoamingCleanup := RoamingCleanupChosen;
  PreservedRestoreOk := False;
  LeftoverDeleteFailed := '';
  LivePath := RoamingOtpFolder() + '\wordslist\words.json';
  Stamp := GetDateTimeString('yyyymmddhhnnss', #0, #0);

  { <261003_1.1>(7.3.2) "기존의 단어 목록 계속 사용(유지)"를 골랐으면, AppData 정리 전에 바탕화면 임시 폴더
    OTP_words_tmp_(날짜시각)에 임시 사본을 만든다. 설치가 끝나면 제자리로 되돌린다(ssPostInstall). }
  if KeepWordsJson then
  begin
    if FileExists(LivePath) then
    begin
      PreservedBackupDir := CopyWordsJsonToNewDesktopDir('OTP_words_tmp_' + Stamp, LivePath, Attempted);
      if PreservedBackupDir = '' then
      begin
        { (7.3.2.2) 다시 묻지 않는다. AppData 의 기존 폴더를 지우지 않고(KeepWordsJson 은 True 로 두어 새 목록으로
          덮어쓰지도 않고) 안내만 한다. 빈 폴더는 위 함수가 이미 지웠다(7.3.2.3). }
        MsgBox('기존의 단어 목록 사본 파일을 만들기에 실패했습니다.' + #13#10 + #13#10 +
               '기존의 단어 목록을 유지하기 위해, 이번 설치에서는 AppData 안의 기존 폴더를 지우지 않습니다.',
               mbError, MB_OK);
        PendingRoamingCleanup := False;
      end;
    end
    else
      KeepWordsJson := False;   { 유지할 파일이 없으면 기본값을 설치한다 }
  end;

  { <261003_1>(7.5)·<261003_1.1>(7.5.1) 단계 개정판 번호가 달라 유지할 수 없을 때: 바탕화면 OTP_words_보존_(날짜시각)
    폴더에 사본을 남기고 새 목록을 설치한다. 사본을 만들지 못하면 선택창을 띄운다. }
  if StageVersionMismatch and FileExists(LivePath) then
  begin
    StageMismatchBackupDir := CopyWordsJsonToNewDesktopDir('OTP_words_보존_' + Stamp, LivePath, Attempted);
    if StageMismatchBackupDir <> '' then
    begin
      { 사본을 만든 뒤 성공했을 때 안내한다. 새 목록의 단계 개정판 번호가 더 낮으면(낮은 버전 설치) 첫 줄을 바꾼다. }
      FirstLine := '자리연습 단계 구성이 바뀌어 PC에 있는 기존의 단어 목록을 그대로 쓸 수 없습니다.';
      if LoadStringFromFile(LivePath, LiveContent) and
         LoadStringFromFile(ExpandConstant('{tmp}\words.json'), PackagedContent) and
         PackagedStageVersionIsLower(LiveContent, PackagedContent) then
        FirstLine := '설치하려는 버전의 자리연습 단계 구성이 PC에 있는 것보다 예전 것이라, PC에 있는 기존의 단어 목록을 그대로 쓸 수 없습니다.';
      { 문단 안에는 줄바꿈을 넣지 않는다 — 안내 창이 스스로 줄을 바꾸므로, 직접 넣은 줄바꿈과 겹쳐 줄이 어색하게
        끊긴다(2026-10-05 썰렁이 시험 3에서 "…폴더에 / 사본으로…"로 확인). }
      MsgBox(FirstLine + #13#10 + #13#10 +
             '새 단어 목록을 설치하고, 고치신 기존의 단어 목록은 바탕화면의 "OTP_words_보존_(날짜시각)" 폴더에 ' +
             '사본으로 남겨 둡니다. 필요한 단어는 이 사본에서 새 단어 목록으로 직접 옮겨 넣으세요.',
             mbInformation, MB_OK);
    end
    else
    begin
      case ShowStageCopyFailedPrompt(Attempted) of
        1: ;   { 새 단어 목록을 설치한다 — 고친 목록은 사라진다 }
        2: begin
             { 새 단어 목록을 설치하지 않고 기존의 단어 목록을 그대로 둔다(AppData 의 그 폴더도 지우지 않는다). }
             KeepWordsJson := True;
             PendingRoamingCleanup := False;
           end;
      else
        begin
          RemoveDesktopCopiesBeforeInstall();
          Result := CancelMessage;   { (7.5.1.5) }
          Exit;
        end;
      end;
    end;
  end;

  { <261005_2> 남는 파일 안내와 삭제(맨 마지막). }
  Result := HandleLeftovers();
  if Result <> '' then
    RemoveDesktopCopiesBeforeInstall();
end;

{ 체크박스가 있던 폼은 이미 닫힌 뒤라 컨트롤이 아니라 LaunchAfterFinish 변수를 읽는다.
  Inno 공식 예제(DeinitializeSetup + Exec)와 같은 패턴이다.
  <261003_1.1>: 실제 설치가 시작되기 전에 끝났으면 이번에 만든 바탕화면 사본 폴더를 지우고(7.3.2.3)(7.5.1.6.1),
  실제 설치가 시작된 뒤 중간에 끝나 임시 폴더를 남기게 되면 그 위치와 다시 쓰는 법을 알린다(7.3.2.1.1). }
procedure DeinitializeSetup();
var
  ResultCode: Integer;
begin
  if not InstallStarted then
    RemoveDesktopCopiesBeforeInstall()
  else if (not PostInstallReached) and (PreservedBackupDir <> '') and DirExists(PreservedBackupDir) then
    MsgBox(KeptTempCopyMessage('설치가 끝까지 진행되지 못했습니다.'), mbError, MB_OK);

  { 설치가 중간에 끝나 ssDone 에 못 갔으면, ssInstall 에서 unins000 으로 되돌려 둔 예전 삭제 프로그램의 이름을
    다시 설치삭제로 돌려놓는다(그래야 사용자가 늘 쓰던 설치삭제.exe 로 지울 수 있다). }
  if UninstallerRestoredForAppend and not UninstallerNameDone then
    RenameUninstallerPair(
      ExpandConstant('{app}\설치삭제\unins000.exe'), ExpandConstant('{app}\설치삭제\unins000.dat'),
      ExpandConstant('{app}\설치삭제\설치삭제.exe'), ExpandConstant('{app}\설치삭제\설치삭제.dat'));

  if LaunchAfterFinish then
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

{ <260830_2-4-2>: "준비 완료" 페이지(라이선스 동의 후 바로 나오는 페이지 — DisableDirPage=yes라
  별도의 "대상 위치 선택" 페이지가 없다)에 실제 설치될 `OTP 폴더` 경로를 안내한다.
  MemoDirInfo는 DisableDirPage=yes일 때 채워진다는 보장이 없어 신뢰하지 않고, (4)/(4-1)에서
  이미 확정해 둔 FinalInstallDir을 직접 쓴다. 이 설치본은 구성 요소·작업·프로그램 그룹 페이지가
  전부 꺼져 있어(MemoTypeInfo/MemoComponentsInfo/MemoGroupInfo/MemoTasksInfo가 항상 비어 있음)
  기본 메모를 완전히 대체해도 안내가 사라지는 항목이 없다. }
function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '설치 위치(OTP 폴더):' + NewLine + Space + FinalInstallDir;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  PackagedContent: AnsiString;
  PreservedPath: String;
begin
  if CurStep = ssInstall then
  begin
    InstallStarted := True;
    PrepareUninstallerForAppend();
    if NeedsDotNetRuntime() then
      ExtractTemporaryFile('{#DotNetInstallerFileName}');

    // <260831 검토>: 예약해 둔 삭제를 여기서 — 설치가 확정된 뒤에 — 실행한다.
    // "기존의 단어 목록 계속 사용(유지)"를 골랐으면 [설치]를 누른 직후(PrepareToInstall)에 이미 바탕화면 임시
    // 폴더에 사본을 만들어 두었고(<261003_1.1>(7.3.2)), ssPostInstall에서 제자리로 되돌린다(삭제 대상 폴더 안에
    // 있어 그냥 두면 같이 지워진다). 사본을 만들지 못했으면 PrepareToInstall 이 PendingRoamingCleanup 을 껐다.
    if PendingRoamingCleanup and DirExists(RoamingOtpFolder()) then
      DelTree(RoamingOtpFolder(), True, True, True);
    if PendingLocalCleanup and DirExists(LocalOtpFolder()) then
      DelTree(LocalOtpFolder(), True, True, True);
    { <260927_15.1>: 바탕화면 OTP 폴더는 더 이상 통째로 지우지 않는다 — [Files]가 같은 이름의
      파일·폴더만 덮어쓰고(ignoreversion, onlyifdoesntexist 없음) 무관한 파일은 그대로 둔다. }
  end;

  if CurStep = ssPostInstall then
  begin
    PostInstallReached := True;
    { "기존의 단어 목록 계속 사용(유지)"를 골랐으면 [Files]가 words.json을 안 깔았으므로
      (Check: ShouldInstallWordsJson), 바탕화면 임시 폴더에 만들어 둔 사본을 제자리에 되돌려 놓는다. }
    if KeepWordsJson and (PreservedBackupDir <> '') then
    begin
      PreservedPath := PreservedBackupDir + '\words.json';
      if FileExists(PreservedPath) then
      begin
        if ForceDirectories(RoamingOtpFolder() + '\wordslist') and
           FileCopy(PreservedPath, RoamingOtpFolder() + '\wordslist\words.json', False) then
          PreservedRestoreOk := True
        else
          { <261003_1.1>(7.3.2.1) 임시 폴더를 지우지 않고 그 위치를 알린다(설치가 끝나도 지워지지 않는다). }
          MsgBox(KeptTempCopyMessage('기존의 단어 목록 사본을 제자리에 되돌려 놓지 못했습니다.'), mbError, MB_OK);
      end;
    end;

    { <261003_1>(7.5): 단계 구성이 바뀌어 새 목록을 깐 경우, 고친 목록의 사본이 있는 곳을 알려 준다
      (이 폴더는 설치가 끝나도 지우지 않는다). }
    if StageMismatchBackupDir <> '' then
      MsgBox('고치신 기존의 단어 목록 사본을 아래 폴더에 남겨 두었습니다:' + #13#10 + StageMismatchBackupDir,
             mbInformation, MB_OK);

    { <261005_2>(7.2) 남는 파일 중 지우는 도중에 지우지 못한 파일이 있었으면 알린다. }
    if LeftoverDeleteFailed <> '' then
      MsgBox('다음 파일은 지우지 못했습니다. 프로그램이 함께 읽을 수 있으니 직접 지워 주세요.' + #13#10 +
             AddBackslash(FinalInstallDir) + #13#10 + LeftoverDeleteFailed, mbError, MB_OK);

    { <260830_2-3>: 신원 마커를 이번 설치가 끝나면서 로밍/로컬 둘 다에 새로 씀
      (예전 버전 설치본이었어도 이번 설치로 신원이 채워짐). }
    WriteOtpMarker(RoamingOtpFolder());
    WriteOtpMarker(LocalOtpFolder());
    // <260830_2-4>(5): 삭제 프로그램이 나중에 비교할 원본 사본을 Local에 별도 저장.
    // "기존의 단어 목록 계속 사용(유지)"를 골라 실제 words.json은 안 새로 깔렸어도, 이번 패키지의 기본값은
    // tmp 폴더에 항상 있으므로 그걸 그대로 옮겨 적는다.
    ForceDirectories(LocalOtpFolder() + '\wordslist');
    if LoadStringFromFile(ExpandConstant('{tmp}\words.json'), PackagedContent) then
      SaveStringToFile(LocalOtpFolder() + '\wordslist\words-original.json', PackagedContent, False);
  end;

  if CurStep = ssDone then
  begin
    RenameUninstaller();
    { <261003_1.1>(7.3.2.1) 되돌리기까지 확실히 끝났으면 바탕화면 임시 폴더를 지운다. 실패했다면 위 안내가
      가리키는 그 폴더이므로 절대 지우지 않는다. }
    if PreservedRestoreOk and (PreservedBackupDir <> '') then
      DelTree(PreservedBackupDir, True, True, True);
  end;
end;

{ ============== <260830_2-5>: 삭제 프로그램(설치삭제.exe) 실행 시 words.json 처리 ============== }
function InitializeUninstall(): Boolean;
var
  LiveContent, OriginalContent: AnsiString;
  LivePath, OriginalPath, PreserveDir, AttemptedDir: String;
  Preserve: Boolean;
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  PreserveButton, DiscardButton: TNewButton;
  Bottom: Integer;
begin
  Result := True;
  LivePath := ExpandConstant('{userappdata}\OTP\OpenTypingPlus\wordslist\words.json');
  OriginalPath := ExpandConstant('{localappdata}\OTP\OpenTypingPlus\wordslist\words-original.json');

  if not FileExists(LivePath) or not FileExists(OriginalPath) then Exit;
  if not LoadStringFromFile(LivePath, LiveContent) then Exit;
  if not LoadStringFromFile(OriginalPath, OriginalContent) then Exit;
  if LiveContent = OriginalContent then Exit; { 원본과 같으면 그냥 삭제 진행 }

  Preserve := False;
  { <260830_2-5-1>: 아래 두 버튼의 캡션이 길어(14자/11자) 기존 420 너비·23 높이에선 문구가
    잘렸다(실제 화면에서 확인됨). 상하·좌우 모두 여유 있게 넓힌다. }
  ConfirmForm := CreateCustomForm(520, 170, True, True);
  try
    ConfirmForm.Caption := 'Open Typing Plus';

    MsgLabel := TNewStaticText.Create(ConfirmForm);
    MsgLabel.Parent := ConfirmForm;
    MsgLabel.Left := 16;
    MsgLabel.Top := 16;
    MsgLabel.AutoSize := False;
    MsgLabel.WordWrap := True;
    MsgLabel.Caption :=
      '기존 설치된 것 중에서 자리연습·산성비 오락의 단어 목록이 변경되어 있습니다.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    PreserveButton := TNewButton.Create(ConfirmForm);
    PreserveButton.Parent := ConfirmForm;
    PreserveButton.Caption := '고친 단어 목록 보존하기';
    { 설치 때 창들(WizardForm.CancelButton.Height)과 같이 화면 배율에 맞춘다. 예전 고정값 28은 배율이 큰 화면에서
      글자가 위아래 테두리에 닿았다(2026-10-05 썰렁이 시험 7에서 확인). 설치삭제 때는 WizardForm 이 없어
      Inno 기본 버튼 높이 23을 ScaleY 로 늘려 쓴다. }
    PreserveButton.Height := ScaleY(23);
    PreserveButton.Width := TextButtonWidth(PreserveButton.Font, PreserveButton.Caption, 40, 75);
    PreserveButton.ModalResult := mrYes;
    PreserveButton.Default := True;   { <261003_1.1> 설치삭제일 때는 보존하기가 기본 버튼 }
    PreserveButton.Cancel := True;    { Esc도 보존(안전) 쪽으로 }

    DiscardButton := TNewButton.Create(ConfirmForm);
    DiscardButton.Parent := ConfirmForm;
    DiscardButton.Caption := '고친 단어 목록 보존하지 않기';
    DiscardButton.Height := ScaleY(23);
    DiscardButton.Width := TextButtonWidth(DiscardButton.Font, DiscardButton.Caption, 40, 75);
    DiscardButton.ModalResult := mrNo;

    { 라벨의 실측 높이에 맞춰 버튼을 놓고, 창 너비를 넘길 것 같으면 상/하로 둔다. }
    Bottom := LayoutTwoButtons(ConfirmForm, PreserveButton, DiscardButton, MsgLabel.Top + MsgLabel.Height + 16);
    ConfirmForm.ClientHeight := Bottom + 10;
    ConfirmForm.ActiveControl := PreserveButton;

    ConfirmForm.Position := poScreenCenter;

    Preserve := (ConfirmForm.ShowModal() <> mrNo);   { X/Esc = 보존(안전) 쪽 }
  finally
    ConfirmForm.Free();
  end;

  if Preserve then
  begin
    { 프로그램을 지우면서 고친 words.json 을 바탕화면 OTP_words_보존_(날짜시각) 폴더에 사본으로 남긴다(AppData 의
      원본은 [UninstallDelete]가 폴더째 지운다). <261003_1.1>(7.7): 같은 이름의 폴더나 파일이 이미 있으면 ' (2)'부터
      번호를 붙이고, 폴더를 만들었지만 복사에 실패해 비어 있으면 삭제를 중단하기 전에 그 빈 폴더를 지운다.
      <260831 검토>: 예전엔 두 호출의 실패를 무시해서, 바탕화면에 쓰지 못하면(디스크 부족,
      "제어된 폴더 액세스" 차단 등) 보존을 골랐는데도 아무 경고 없이 원본이 삭제됐다. 사본을
      확실히 만들지 못하면 삭제 자체를 중단한다. }
    PreserveDir := CreateUniqueDesktopDir('OTP_words_보존_' + GetDateTimeString('yyyymmddhhnnss', #0, #0), AttemptedDir);
    if PreserveDir = '' then
    begin
      MsgBox('기존의 단어 목록을 보존할 폴더를 만들지 못했습니다:' + #13#10 + AttemptedDir + #13#10 + #13#10 +
             '기존의 단어 목록이 삭제되지 않도록 삭제를 중단합니다.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not FileCopy(LivePath, PreserveDir + '\words.json', False) then
    begin
      { 이번에 새로 만든 폴더라 그 안에는 복사하다 만 사본 말고는 없다. }
      DeleteFile(PreserveDir + '\words.json');
      RemoveDir(PreserveDir);
      MsgBox('기존의 단어 목록 사본을 만들지 못했습니다:' + #13#10 + PreserveDir + '\words.json' + #13#10 + #13#10 +
             '기존의 단어 목록이 삭제되지 않도록 삭제를 중단합니다.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;
