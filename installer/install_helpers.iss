{ <261005_1>·<261005_2>·<261003_1.1> 설치 프로그램이 쓰는 순수 함수(파일을 읽거나 창을 띄우지 않는 것)만 둔다 —
  installer.iss 가 #include 로 쓰고, installer\test_words_decision.iss 가 같은 함수를 따로 시험한다. }

{ 'a.b.c.d' 형식 버전의 Index번째(0부터) 자리. 자리가 없거나 숫자가 아니면 0. }
function VersionPart(const S: String; Index: Integer): Integer;
var
  I, Part: Integer;
  Digits: String;
begin
  Part := 0;
  Digits := '';
  for I := 1 to Length(S) do
  begin
    if S[I] = '.' then
    begin
      if Part = Index then Break;
      Part := Part + 1;
      Digits := '';
    end
    else if Part = Index then
      Digits := Digits + S[I];
  end;
  if Part < Index then
    Result := 0
  else
    Result := StrToIntDef(Trim(Digits), 0);
end;

{ <261005_1>(2.1) 네 자리 버전을 자리별로 숫자로 비교한다(글자로 비교하지 않는다 — 1.10.0.0 은 1.9.0.0 보다 높다).
  반환: A > B 면 1, 같으면 0, A < B 면 -1. }
function CompareVersionStrings(const A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 0 to 3 do
  begin
    X := VersionPart(A, I);
    Y := VersionPart(B, I);
    if X > Y then begin Result := 1; Exit; end;
    if X < Y then begin Result := -1; Exit; end;
  end;
end;

{ <261005_2>(2) Manifest 는 이번 설치 파일에 든 파일의 상대 경로를 '|'로 이어 붙이고 끝에도 '|'를 둔 목록
  (예: 'stages\a.json|hands\b.svg|'). Rel 이 그 안에 있는가. Windows 처럼 대소문자를 구분하지 않는다. }
function ManifestHas(const Manifest, Rel: String): Boolean;
begin
  Result := Pos('|' + AnsiLowercase(Rel) + '|', '|' + AnsiLowercase(Manifest)) > 0;
end;

{ <261003_1.1>(7.3)(7.5)(7.7) 바탕화면 폴더 이름: N 이 1 이하이면 Base 그대로, 2 이상이면 Base + ' (N)'. }
function NumberedName(const Base: String; N: Integer): String;
begin
  if N <= 1 then
    Result := Base
  else
    Result := Base + ' (' + IntToStr(N) + ')';
end;
