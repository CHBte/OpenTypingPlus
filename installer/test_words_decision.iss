; <261003_1>(9.3) 재설치 때 단어 목록 판단(words_decision.iss)만 따로 시험하는 작은 설치 스크립트.
; 실제로 설치하지 않는다 — InitializeSetup 에서 시험 결과를 파일에 쓰고 False 를 돌려 바로 끝낸다
; (AppData·바탕화면 등 사용자 파일을 건드리지 않는다).
;
; 쓰는 법(개발용):
;   ISCC test_words_decision.iss /O<출력폴더>
;   <출력폴더>\test_words_decision.exe /VERYSILENT /SUPPRESSMSGBOXES /RESULT=<결과 txt 경로>
; 결과 txt 의 마지막 줄이 "WORDS DECISION: PASS" 면 통과.

[Setup]
AppName=OTP words decision test
AppVersion=0
DefaultDirName={tmp}\otp_words_decision_test
OutputBaseFilename=test_words_decision
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest

[Code]
#include "words_decision.iss"
#include "install_helpers.iss"

var
  Report: String;
  Fails: Integer;

procedure Check(Name: String; Actual, Expected: Integer);
begin
  if Actual = Expected then
    Report := Report + 'PASS ' + Name + #13#10
  else
  begin
    Report := Report + 'FAIL ' + Name + ' (결과 ' + IntToStr(Actual) + ', 기대 ' + IntToStr(Expected) + ')' + #13#10;
    Fails := Fails + 1;
  end;
end;

function InitializeSetup(): Boolean;
var
  V1, V1Edited, V2, V2Edited, OldFormat, NoVer, OutPath, KoPath: String;
  KoLive, KoPackaged: AnsiString;
  P: Integer;
begin
  Report := '';
  Fails := 0;
  V1       := '{"stage_version": 1, "hangul": {"stages": []}, "w": "a"}';
  V1Edited := '{"stage_version": 1, "hangul": {"stages": []}, "w": "a,b"}';
  V2       := '{"stage_version": 2, "hangul": {"stages": []}, "w": "a"}';
  V2Edited := '{"stage_version" : 2 , "hangul": {"stages": []}, "w": "a,b"}';
  NoVer    := '{"hangul": {"stages": []}, "w": "a,b"}';
  OldFormat := '{"stages": [], "w": "x"}';

  Check('단계 개정판 번호 읽기(1)', WordsStageVersion(V1), 1);
  Check('단계 개정판 번호 읽기(빈칸·2)', WordsStageVersion(V2Edited), 2);
  Check('단계 개정판 번호 없으면 1', WordsStageVersion(NoVer), 1);

  { (7.2) 고치지 않음(원본과 같음) → 묻지 않고 새 목록 }
  Check('안 고침·새 버전 목록 바뀜 → 0', DecideWordsJson(V1, V1Edited, V1, True), 0);
  { (7.3) 고침 + 번호 같음 → 묻는다 }
  Check('고침·번호 같음 → 1', DecideWordsJson(V1Edited, V1, V1, True), 1);
  { (7.5) 고침 + 번호 다름 → 유지 불가 }
  Check('고침·번호 다름 → 2', DecideWordsJson(V1Edited, V2, V1, True), 2);
  { 번호 없는 옛 목록(=1)을 고쳤고 새 목록은 2 → 유지 불가 }
  Check('번호 없는 고친 목록·새 번호 2 → 2', DecideWordsJson(NoVer, V2, V1, True), 2);
  { 사용자 목록이 새 목록과 똑같음 → 0 }
  Check('새 목록과 같음 → 0', DecideWordsJson(V2, V2, V1, True), 0);
  { (7.1) 원본 사본 없음 → 예전 방식(새 패키지와 다르면 고친 것) }
  Check('원본 없음·새 목록과 다름·번호 같음 → 1', DecideWordsJson(V1, V1Edited, '', False), 1);
  Check('원본 없음·새 목록과 다름·번호 다름 → 2', DecideWordsJson(V1, V2, '', False), 2);
  { (7.6) 예전 형식("hangul" 없음) → 0 }
  Check('예전 형식 → 0', DecideWordsJson(OldFormat, V2, OldFormat, True), 0);

  { 실제 words.json 처럼 한글이 든 UTF-8 파일을 바이트 그대로 읽었을 때도 번호를 바르게 읽는가.
    (2026-10-05: Pos 가 CP949 로 바꿔 찾아 위치가 어긋나, 실제 파일의 번호 2를 1로 읽은 버그가 있었다.) }
  { 저장소의 실제 words.json(한글이 든 UTF-8, 번호 1)을 읽고, 그 바이트에서 번호만 2로 바꾼 것을 사용자 목록으로 쓴다. }
  KoPath := '{#SourcePath}..\OpenTyping\wordslist\words.json';
  if not LoadStringFromFile(KoPath, KoPackaged) then
  begin
    Report := Report + 'FAIL 실제 words.json 을 읽지 못함: ' + KoPath + #13#10;
    Fails := Fails + 1;
  end;
  KoLive := KoPackaged;
  P := BytePos('"stage_version": 1', KoLive);
  if P > 0 then KoLive[P + Length('"stage_version": ')] := '2';
  Check('한글 UTF-8 파일의 번호 2', WordsStageVersion(KoLive), 2);
  Check('한글 UTF-8 파일의 번호 1', WordsStageVersion(KoPackaged), 1);
  Check('한글 UTF-8: 고침·번호 다름 → 2', DecideWordsJson(KoLive, KoPackaged, KoPackaged, True), 2);
  Check('한글 UTF-8: 새 번호가 낮음', Ord(PackagedStageVersionIsLower(KoLive, KoPackaged)), 1);

  { <261003_1.1> (7.5) 안내의 단서: 새 목록의 단계 개정판 번호가 더 낮은가 }
  Check('새 번호 1 < PC 번호 2 → 낮음', Ord(PackagedStageVersionIsLower(V2Edited, V1)), 1);
  Check('새 번호 2 > PC 번호 1 → 낮지 않음', Ord(PackagedStageVersionIsLower(V1Edited, V2)), 0);
  Check('번호 같음 → 낮지 않음', Ord(PackagedStageVersionIsLower(V1, V1Edited)), 0);

  { <261005_1>(2.1) 버전은 자리별 숫자 비교 }
  Check('1.10.0.0 > 1.9.0.0', CompareVersionStrings('1.10.0.0', '1.9.0.0'), 1);
  Check('1.0.0.0 = 1.0.0.0', CompareVersionStrings('1.0.0.0', '1.0.0.0'), 0);
  Check('1.0.0.0 < 2.0.0.0', CompareVersionStrings('1.0.0.0', '2.0.0.0'), -1);
  Check('1.0.0.9 < 1.0.1.0', CompareVersionStrings('1.0.0.9', '1.0.1.0'), -1);
  Check('1.2 = 1.2.0.0', CompareVersionStrings('1.2', '1.2.0.0'), 0);

  { <261005_2>(2) 설치 파일 목록에 있는가(대소문자 무시, 부분 일치 아님) }
  Check('목록에 있음', Ord(ManifestHas('stages\a.json|hands\b.svg|', 'stages\a.json')), 1);
  Check('대소문자 무시', Ord(ManifestHas('stages\a.json|hands\b.svg|', 'STAGES\A.JSON')), 1);
  Check('없음', Ord(ManifestHas('stages\a.json|hands\b.svg|', 'stages\c.json')), 0);
  Check('부분 일치는 없음', Ord(ManifestHas('stages\aa.json|', 'stages\a.json')), 0);
  Check('끝부분 일치는 없음', Ord(ManifestHas('xstages\a.json|', 'stages\a.json')), 0);

  { <261003_1.1> 바탕화면 폴더 번호 }
  if NumberedName('OTP_words_tmp_1', 1) <> 'OTP_words_tmp_1' then begin Report := Report + 'FAIL 번호 1' + #13#10; Fails := Fails + 1; end
  else Report := Report + 'PASS 번호 1 → 그대로' + #13#10;
  if NumberedName('OTP_words_tmp_1', 2) <> 'OTP_words_tmp_1 (2)' then begin Report := Report + 'FAIL 번호 2' + #13#10; Fails := Fails + 1; end
  else Report := Report + 'PASS 번호 2 → " (2)"' + #13#10;

  if Fails = 0 then Report := Report + 'WORDS DECISION: PASS' + #13#10
  else Report := Report + 'WORDS DECISION: FAIL (' + IntToStr(Fails) + '건)' + #13#10;

  OutPath := ExpandConstant('{param:RESULT|}');
  if OutPath <> '' then SaveStringToFile(OutPath, Report, False);
  Result := False;   { 설치하지 않고 끝낸다 }
end;
