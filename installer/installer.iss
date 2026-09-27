; OpenTypingPlus 위치 선택형 SFX 설치 스크립트 (<260830_2-4> 전면 재구현)
; build-exe.bat이 이 파일을 ISCC.exe로 컴파일한다. 사람이 직접 컴파일하려면 Inno Setup IDE로
; 이 파일을 열고 컴파일(Ctrl+F9)하면 되며, 그 경우 아래 기본값(버전/닷넷 설치 파일 경로)이 쓰인다.
;
; 전제: build.bat을 먼저 실행해 "..\build\" 아래에 열린타자+.exe와
; layouts\ data\ wordslist\ stages\ hands\ 폴더가 이미 만들어져 있어야 한다.

#ifndef MyAppVersion
  #define MyAppVersion "0.5.0.0"
#endif

#define MyAppName "Open Typing Plus"
#define MyAppExeName "열린타자+.exe"
#define DotNetInstallerFileName "windowsdesktop-runtime-10.0.11-win-x64.exe"

#ifndef DotNetInstallerPath
  #define DotNetInstallerPath "..\OpenTyping\obj\installer-cache\" + DotNetInstallerFileName
#endif

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
; (4-1-1)/(4-1-2)에서 사용자가 "보존"을 고르면 ShouldInstallWordsJson이 False가 되어 이 항목을 건너뛴다.
Source: "..\build\wordslist\words.json"; DestDir: "{userappdata}\OTP\OpenTypingPlus\wordslist"; Flags: ignoreversion; Check: ShouldInstallWordsJson
; words.json은 {tmp}에도 항상(조건 없이) 꺼내 둔다 — (4-1-1) 비교와 words-original.json 생성에
; "이번 설치 패키지가 원래 기본값으로 담고 있는 내용"이 필요한데, 그건 사용자가 "보존"을 골라
; 위 항목이 실제로 안 깔릴 수도 있어 그것만으론 알 수 없기 때문이다.
Source: "..\build\wordslist\words.json"; DestDir: "{tmp}"; Flags: dontcopy
; .NET 런타임 설치 파일은 SFX 안에 압축 포함만 하고(dontcopy), 실제로 필요할 때만
; ExtractTemporaryFile로 {tmp}에 꺼낸다 (아래 [Code]의 CurStepChanged 참고).
Source: "{#DotNetInstallerPath}"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
; <260830_2-3>(4-1): 사용자가 AppData에 직접 갈 필요 없이, OTP 폴더 안에서 바로 단어 목록을
; 편집할 수 있게 하는 바로가기. 대상이 AppData 쪽 실제 파일이라 사용자가 더블클릭하면 그 파일이 열린다.
Name: "{app}\wordslist\단어 목록 편집 (words.json)"; Filename: "{userappdata}\OTP\OpenTypingPlus\wordslist\words.json"

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
; words.json/words-original.json의 예외 처리는 아래 [Code]의 CurUninstallStepChanged가
; 이 삭제가 실행되기 전에 미리 다른 곳으로 옮겨 두는 방식으로 처리한다 (<260830_2-5>).
Type: filesandordirs; Name: "{userappdata}\OTP\OpenTypingPlus"
Type: filesandordirs; Name: "{localappdata}\OTP\OpenTypingPlus"

[Code]
var
  { <260830_2-4>(4): 최종 설치 위치. 기본값은 DefaultDirName과 같은 바탕화면\OTP. }
  FinalInstallDir: String;
  { <260830_2-4>(4-1-2): "기존 단어 목록 보존하기"를 골랐으면 True — words.json을 AppData에
    새로 안 쓰고 이미 있는 걸 그대로 둔다. }
  PreserveWordsJson: Boolean;
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
  // <260831 2차 검토>: "보존"을 고른 words.json의 백업 위치. Inno의 임시 폴더(tmp 상수)에 두면
  // 설치가 중간에 취소·실패했을 때 Inno가 그 폴더를 통째로 지우면서 백업본까지 함께 사라져
  // (원본은 이미 DelTree로 없어진 뒤) 데이터가 영구 소실된다. 그래서 설치가 끝나도 남는
  // 바탕화면에 둔다. 복원까지 성공하면 ssDone에서 정리하고, 실패하면 그대로 남겨 둔다.
  PreservedBackupDir: String;
  PreservedRestoreOk: Boolean;
  { <260901_2>: 완료 페이지의 "Open Typing Plus 실행" 체크박스. Inno이 자동 생성하는
    TNewCheckListBox 대신 우리가 직접 만드는 진짜 TNewCheckBox(네이티브 Win32 체크박스라
    DPI에 맞게 그려짐)다. LaunchAfterFinish는 그 체크 상태를 기억해 뒀다가, 폼이 이미 닫힌 뒤
    실행되는 DeinitializeSetup에서 읽는다(그 시점엔 LaunchCheckBox 객체 자체가 이미 해제됐을
    수 있어 컨트롤이 아니라 이 변수를 믿는다). }
  LaunchCheckBox: TNewCheckBox;
  LaunchAfterFinish: Boolean;

const
  OtpAppGuid = '6213C516-00A9-46B3-A63B-4671D3E53731';
  MarkerFileName = '.otp-identity';

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

{ ============== (4-1-1)/(4-1-2): words.json 비교 후 보존 여부 ============== }
function WordsJsonUnchanged(): Boolean;
var
  LiveContent, PackagedContent: AnsiString;
  LivePath, PackagedPath: String;
begin
  Result := True; { 비교 대상 파일이 없으면 "달라진 게 없다"로 취급(=보존 안 해도 됨) }
  LivePath := RoamingOtpFolder() + '\wordslist\words.json';
  PackagedPath := ExpandConstant('{tmp}\words.json'); { 이번 설치 패키지 자체의 기본값 }
  if not FileExists(LivePath) or not FileExists(PackagedPath) then Exit;
  if not LoadStringFromFile(LivePath, LiveContent) then Exit;
  if not LoadStringFromFile(PackagedPath, PackagedContent) then Exit;
  { <260927_2> 제시어 목록 형식이 바뀌었다(자리연습·오락 공용, "hangul"/"english" 묶음). 예전 형식의
    words.json 은 새 프로그램이 읽지 못해(내장 예비본 10개씩만 쓰게 됨) 보존할 의미가 없으므로,
    내용이 달라도 '보존할 것 없음'으로 보고 새 목록을 깐다. }
  if Pos('"hangul"', LiveContent) = 0 then Exit;
  Result := (LiveContent = PackagedContent);
end;

{ (4-1-2) 문구·버튼. 반환: True = 보존, False = 보존 안 함 }
function ShowWordsJsonPreservePrompt(): Boolean;
var
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  KeepButton, DiscardButton: TNewButton;
  NeededHeight: Integer;
begin
  { <260830_2-4-3>(2): 아래 두 버튼의 캡션이 길어(14자/11자) 기존 420 너비에선 문구가 잘렸다
    (실제 화면에서 확인됨). 여유 있게 넓힌다. }
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
      '기존 설치된 것 중 자리연습·산성비 오락의 단어 목록(OTP 폴더\wordslist\words.json)이 변경되어' + #13#10 +
      '있습니다. 기존의 단어 목록이 설치하려는 단어 목록보다 최신일 수도 있습니다.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    DiscardButton := TNewButton.Create(ConfirmForm);
    DiscardButton.Parent := ConfirmForm;
    DiscardButton.Caption := '기존 단어 목록 보존하지 않기';
    DiscardButton.Height := WizardForm.CancelButton.Height;
    DiscardButton.Width := TextButtonWidth(DiscardButton.Font, DiscardButton.Caption, 40, 75);
    { 라벨의 실측 높이가 원래 여유 공간보다 클 수 있으니, 버튼 행과 겹치지 않게 폼 높이를
      필요하면 늘린다. 버튼은 ClientHeight 기준 상대 좌표라 자동으로 같이 밀려난다. }
    NeededHeight := MsgLabel.Top + MsgLabel.Height + 16 + DiscardButton.Height + 10;
    if NeededHeight > ConfirmForm.ClientHeight then
      ConfirmForm.ClientHeight := NeededHeight;
    DiscardButton.Top := ConfirmForm.ClientHeight - DiscardButton.Height - 10;
    DiscardButton.Left := ConfirmForm.ClientWidth - DiscardButton.Width - 16;
    DiscardButton.ModalResult := mrNo;

    KeepButton := TNewButton.Create(ConfirmForm);
    KeepButton.Parent := ConfirmForm;
    KeepButton.Caption := '기존 단어 목록 보존하기';
    KeepButton.Height := WizardForm.CancelButton.Height;
    KeepButton.Width := TextButtonWidth(KeepButton.Font, KeepButton.Caption, 40, 75);
    KeepButton.Top := DiscardButton.Top;
    KeepButton.Left := DiscardButton.Left - KeepButton.Width - 8;
    KeepButton.ModalResult := mrYes;
    KeepButton.Default := True;
    KeepButton.Cancel := True;   { Esc도 보존(안전) 쪽으로 }
    ConfirmForm.ActiveControl := KeepButton;

    ConfirmForm.Left := WizardForm.Left + (WizardForm.Width - ConfirmForm.Width) div 2;
    ConfirmForm.Top := WizardForm.Top + (WizardForm.Height - ConfirmForm.Height) div 2;

    Result := (ConfirmForm.ShowModal() <> mrNo);   { X/Esc = 보존(안전) 쪽 }
  finally
    ConfirmForm.Free();
  end;
end;

function ShouldInstallWordsJson(): Boolean;
begin
  Result := not PreserveWordsJson;
end;

{ ============== 이벤트 함수 ============== }
{ <260830_2-2> 자동 생성된 삭제 프로그램(unins000.exe / .dat)의 이름을 "설치삭제.exe"로
  바꾼다. 두 파일을 같이 바꿔야 한다 — 삭제 프로그램은 자기 이름과 같은 이름의 .dat에서
  삭제할 파일 목록을 읽으므로, 이름이 서로 안 맞으면 못 찾는다. 제일 마지막 단계(ssDone)에서
  해야 그 시점엔 두 파일이 확실히 다 만들어져 있다. }
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
  if FileExists(OldExe) then
    RenameFile(OldExe, NewExe);
  if FileExists(OldDat) then
    RenameFile(OldDat, NewDat);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  { 라이선스 페이지에서 "다음"을 눌러 넘어가는 시점 = 라이선스에 동의한 시점
    (Inno Setup이 "동의하지 않음" 상태에서는 애초에 다음으로 못 넘어가게 막아준다).
    여기서 (3) AppData 신원 확인과 (4) 바탕화면 OTP 폴더 확인을 순서대로 처리한다.
    DisableDirPage=yes라 별도의 "대상 위치 선택" 페이지가 없으므로 전부 여기서 처리한다. }
  if CurPageID = wpLicense then
  begin
    InstallCancelled := False;
    FinalInstallDir := ExpandConstant('{autodesktop}\OTP');
    PreserveWordsJson := False;
    PendingRoamingCleanup := False;
    PendingLocalCleanup := False;
    PreservedBackupDir := '';
    PreservedRestoreOk := False;
    // (4-1-1) 비교, words-original.json 생성 어느 쪽이든 "이번 설치 패키지 자체의 기본
    // words.json 내용"이 필요하므로, 여기서 미리(조건 없이) tmp 폴더에 꺼내 둔다.
    ExtractTemporaryFile('words.json');

    { <260831 검토>(4-1-1)/(4-1-2): 이 비교를 여기서 — 어떤 삭제보다도 먼저 — 한다.
      예전엔 (3-1)의 DelTree가 라이브 words.json을 먼저 지운 뒤에야 비교가 돌아서,
      FileExists가 항상 False → "달라진 것 없음"으로 판정 → 보존 프롬프트가 정상 재설치
      경로에서는 아예 뜨지 않았고 사용자가 고친 단어 목록이 매번 사라졌다. }
    if not WordsJsonUnchanged() then
      PreserveWordsJson := ShowWordsJsonPreservePrompt();

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

    if DesktopOtpHasFiles() then
    begin
      case ShowDesktopConflictPrompt() of
        0: begin { 취소 } Result := False; WizardForm.Close(); Exit; end;
        1: begin { 덮어쓰기 설치 — [Files]가 같은 이름의 파일만 덮어쓰므로 여기선 할 일이 없다 } end;
        2: begin { 사용자 지정 경로에 설치 - FinalInstallDir는 ShowDesktopConflictPrompt 안에서 이미 설정됨 } end;
      end;
    end;

    WizardForm.DirEdit.Text := FinalInstallDir;
  end;
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

{ 체크박스가 있던 폼은 이미 닫힌 뒤라 컨트롤이 아니라 LaunchAfterFinish 변수를 읽는다.
  Inno 공식 예제(DeinitializeSetup + Exec)와 같은 패턴이다. }
procedure DeinitializeSetup();
var
  ResultCode: Integer;
begin
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
  LivePath, PreservedPath: String;
begin
  if CurStep = ssInstall then
  begin
    if NeedsDotNetRuntime() then
      ExtractTemporaryFile('{#DotNetInstallerFileName}');

    // <260831 검토>: 예약해 둔 삭제를 여기서 — 설치가 확정된 뒤에 — 실행한다.
    // "보존"을 골랐으면 삭제 전에 라이브 words.json을 바탕화면 보존 폴더로 복사해 두었다가
    // ssPostInstall에서 제자리로 되돌린다(삭제 대상 폴더 안에 있어 그냥 두면 같이 지워진다).
    // 백업을 Inno 임시 폴더가 아니라 바탕화면에 두는 이유는 위 PreservedBackupDir 주석 참고.
    if PreserveWordsJson then
    begin
      LivePath := RoamingOtpFolder() + '\wordslist\words.json';
      if FileExists(LivePath) then
      begin
        PreservedBackupDir := ExpandConstant('{userdesktop}\OTP_words_보존_') +
                              GetDateTimeString('yyyymmddhhnnss', #0, #0);
        PreservedPath := PreservedBackupDir + '\words.json';
        if (not ForceDirectories(PreservedBackupDir)) or (not FileCopy(LivePath, PreservedPath, False)) then
        begin
          { 백업을 못 뜨면 보존 약속을 지킬 수 없다. 그렇다고 원본을 지우고 기본값을 깔면
            사용자 데이터가 사라지므로, 아예 삭제를 하지 않는 쪽으로 물러선다. }
          MsgBox('기존 단어 목록의 사본을 만들지 못했습니다:' + #13#10 + PreservedBackupDir + #13#10 + #13#10 +
                 '기존 단어 목록이 사라지지 않도록, 이번 설치에서는 AppData의 기존 폴더를 지우지 않고' + #13#10 +
                 '단어 목록도 그대로 둡니다.', mbError, MB_OK);
          PreservedBackupDir := '';
          PendingRoamingCleanup := False;   { 원본을 지우지 않는다 — PreserveWordsJson은 True로 두어 }
                                            { [Files]가 기본값으로 덮어쓰지도 않게 한다 }
        end;
      end
      else
        PreserveWordsJson := False; { 보존할 파일이 없으면 기본값을 설치한다 }
    end;

    if PendingRoamingCleanup and DirExists(RoamingOtpFolder()) then
      DelTree(RoamingOtpFolder(), True, True, True);
    if PendingLocalCleanup and DirExists(LocalOtpFolder()) then
      DelTree(LocalOtpFolder(), True, True, True);
    { <260927_15.1>: 바탕화면 OTP 폴더는 더 이상 통째로 지우지 않는다 — [Files]가 같은 이름의
      파일·폴더만 덮어쓰고(ignoreversion, onlyifdoesntexist 없음) 무관한 파일은 그대로 둔다. }
  end;

  if CurStep = ssPostInstall then
  begin
    { 보존을 골랐으면 [Files]가 words.json을 안 깔았으므로(Check: ShouldInstallWordsJson),
      위에서 임시 보관해 둔 사용자의 파일을 제자리에 돌려놓는다. }
    if PreserveWordsJson and (PreservedBackupDir <> '') then
    begin
      PreservedPath := PreservedBackupDir + '\words.json';
      if FileExists(PreservedPath) then
      begin
        if ForceDirectories(RoamingOtpFolder() + '\wordslist') and
           FileCopy(PreservedPath, RoamingOtpFolder() + '\wordslist\words.json', False) then
          PreservedRestoreOk := True
        else
          { 사본이 남아 있는 바탕화면 폴더를 알려 준다 — 이 폴더는 설치가 끝나도 지워지지 않는다. }
          MsgBox('기존 단어 목록을 제자리에 되돌려 놓지 못했습니다.' + #13#10 + #13#10 +
                 '사본을 아래 폴더에 그대로 남겨 두었습니다:' + #13#10 + PreservedBackupDir,
                 mbError, MB_OK);
      end;
    end;

    { <260830_2-3>: 신원 마커를 이번 설치가 끝나면서 로밍/로컬 둘 다에 새로 씀
      (예전 버전 설치본이었어도 이번 설치로 신원이 채워짐). }
    WriteOtpMarker(RoamingOtpFolder());
    WriteOtpMarker(LocalOtpFolder());
    // <260830_2-4>(5): 삭제 프로그램이 나중에 비교할 원본 사본을 Local에 별도 저장.
    // "보존"을 골라 실제 words.json은 안 새로 깔렸어도, 이번 패키지의 기본값은
    // tmp 폴더에 항상 있으므로 그걸 그대로 옮겨 적는다.
    ForceDirectories(LocalOtpFolder() + '\wordslist');
    if LoadStringFromFile(ExpandConstant('{tmp}\words.json'), PackagedContent) then
      SaveStringToFile(LocalOtpFolder() + '\wordslist\words-original.json', PackagedContent, False);
  end;

  if CurStep = ssDone then
  begin
    RenameUninstaller();
    { 복원까지 확실히 끝났으면 바탕화면 임시 사본을 치운다. 실패했다면 위 안내가 가리키는
      그 폴더이므로 절대 지우지 않는다. }
    if PreservedRestoreOk and (PreservedBackupDir <> '') then
      DelTree(PreservedBackupDir, True, True, True);
  end;
end;

{ ============== <260830_2-5>: 삭제 프로그램(설치삭제.exe) 실행 시 words.json 처리 ============== }
function InitializeUninstall(): Boolean;
var
  LiveContent, OriginalContent: AnsiString;
  LivePath, OriginalPath, PreserveDir: String;
  Preserve: Boolean;
  ConfirmForm: TSetupForm;
  MsgLabel: TNewStaticText;
  KeepButton, DiscardButton: TNewButton;
  NeededHeight: Integer;
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
      '기존 설치된 것 중 산성비 오락의 단어 목록이 변경되어 있습니다.' + #13#10 +
      '기존의 단어 목록이 설치하려는 단어 목록보다 최신일 수도 있습니다.';
    EnsureFormWideEnoughForLabel(ConfirmForm, MsgLabel.Font, MsgLabel.Caption, 32);
    MsgLabel.Width := ConfirmForm.ClientWidth - 32;
    MsgLabel.Height := TextLineCount(MsgLabel.Caption) * TextLineHeight(MsgLabel.Font) + 6;

    DiscardButton := TNewButton.Create(ConfirmForm);
    DiscardButton.Parent := ConfirmForm;
    DiscardButton.Caption := '기존 단어 목록 보존하지 않기';
    DiscardButton.Height := 28;
    DiscardButton.Width := TextButtonWidth(DiscardButton.Font, DiscardButton.Caption, 40, 75);
    { 라벨의 실측 높이가 원래 여유 공간보다 클 수 있으니, 버튼 행과 겹치지 않게 폼 높이를
      필요하면 늘린다. 버튼은 ClientHeight 기준 상대 좌표라 자동으로 같이 밀려난다. }
    NeededHeight := MsgLabel.Top + MsgLabel.Height + 16 + DiscardButton.Height + 10;
    if NeededHeight > ConfirmForm.ClientHeight then
      ConfirmForm.ClientHeight := NeededHeight;
    DiscardButton.Top := ConfirmForm.ClientHeight - DiscardButton.Height - 10;
    DiscardButton.Left := ConfirmForm.ClientWidth - DiscardButton.Width - 16;
    DiscardButton.ModalResult := mrNo;

    KeepButton := TNewButton.Create(ConfirmForm);
    KeepButton.Parent := ConfirmForm;
    KeepButton.Caption := '기존 단어 목록 보존하기';
    KeepButton.Height := 28;
    KeepButton.Width := TextButtonWidth(KeepButton.Font, KeepButton.Caption, 40, 75);
    KeepButton.Top := DiscardButton.Top;
    KeepButton.Left := DiscardButton.Left - KeepButton.Width - 8;
    KeepButton.ModalResult := mrYes;
    KeepButton.Default := True;
    KeepButton.Cancel := True;   { Esc도 보존(안전) 쪽으로 }
    ConfirmForm.ActiveControl := KeepButton;

    ConfirmForm.Position := poScreenCenter;

    Preserve := (ConfirmForm.ShowModal() <> mrNo);   { X/Esc = 보존(안전) 쪽 }
  finally
    ConfirmForm.Free();
  end;

  if Preserve then
  begin
    { words.json과 그걸 담은 wordslist 폴더만 삭제 대상에서 벗어나게, 삭제 전에 임시로 옮겨 둔다.
      <260831 검토>: 예전엔 두 호출의 실패를 무시해서, 바탕화면에 쓰지 못하면(디스크 부족,
      "제어된 폴더 액세스" 차단 등) 보존을 골랐는데도 아무 경고 없이 원본이 삭제됐다. 사본을
      확실히 만들지 못하면 삭제 자체를 중단한다. }
    PreserveDir := ExpandConstant('{userdesktop}\OTP_words_보존_') + GetDateTimeString('yyyymmddhhnnss', #0, #0);
    if not ForceDirectories(PreserveDir) then
    begin
      MsgBox('단어 목록을 보존할 폴더를 만들지 못했습니다:' + #13#10 + PreserveDir + #13#10 + #13#10 +
             '기존 단어 목록이 삭제되지 않도록 삭제를 중단합니다.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not FileCopy(LivePath, PreserveDir + '\words.json', False) then
    begin
      MsgBox('단어 목록 사본을 만들지 못했습니다:' + #13#10 + PreserveDir + '\words.json' + #13#10 + #13#10 +
             '기존 단어 목록이 삭제되지 않도록 삭제를 중단합니다.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;
