{ <261003_1>(7) 재설치 때 단어 목록(words.json) 처리의 판단 부분. 파일을 읽지 않는 순수 함수만 둔다 —
  installer.iss 가 #include 로 쓰고, installer\test_words_decision.iss 가 같은 함수를 따로 시험한다
  (실제 설치를 돌리지 않고 판단 규칙만 확인하려는 것). }

{ Content(파일을 LoadStringFromFile 로 읽은 바이트 그대로의 UTF-8)에서 Sub(영문·기호만)가 처음 나오는 바이트 위치
  (1부터, 없으면 0). Pos 를 쓰면 AnsiString 이 Windows 기본 문자 체계(CP949)로 바뀌어, 한글이 든 UTF-8 에서는 찾은
  위치가 바이트 위치와 어긋난다(2026-10-05 실제 words.json 으로 확인 — 번호 2를 1로 읽었다). 그래서 바이트끼리 비교한다. }
function BytePos(const Sub: String; const Content: AnsiString): Integer;
var
  I, J, N, M: Integer;
  Match: Boolean;
begin
  Result := 0;
  N := Length(Content);
  M := Length(Sub);
  if M = 0 then Exit;
  for I := 1 to N - M + 1 do
  begin
    Match := True;
    for J := 1 to M do
      if Ord(Content[I + J - 1]) <> Ord(Sub[J]) then
      begin
        Match := False;
        Break;
      end;
    if Match then
    begin
      Result := I;
      Exit;
    end;
  end;
end;

{ <261003_1>(7.4) words.json 의 '단계 개정판 번호'("stage_version": N). 자리연습 단계 구성이 바뀌어 제시어
  목록의 배치가 달라질 때만 오른다. 번호가 없는 words.json 은 1로 본다. }
function WordsStageVersion(Content: AnsiString): Integer;
var
  P, I: Integer;
  Digits: String;
begin
  Result := 1;
  P := BytePos('"stage_version"', Content);
  if P = 0 then Exit;
  I := P + Length('"stage_version"');
  while (I <= Length(Content)) and ((Content[I] = ' ') or (Content[I] = #9) or (Content[I] = #13) or
        (Content[I] = #10) or (Content[I] = ':')) do
    I := I + 1;
  Digits := '';
  while (I <= Length(Content)) and (Content[I] >= '0') and (Content[I] <= '9') do
  begin
    Digits := Digits + Chr(Ord(Content[I]));
    I := I + 1;
  end;
  Result := StrToIntDef(Digits, 1);
end;

{ <261003_1>(7) 재설치 때 단어 목록(words.json) 처리 — 사용자가 고친 목록은 사용자가 결정한다.
  Live = 사용자 PC 의 words.json, Packaged = 이번 설치 패키지의 words.json,
  Original = 이전 설치 때의 원본 사본(words-original.json, HasOriginal 이 False 면 없음).
  반환: 0 = 묻지 않고 새 목록 설치, 1 = 계속 사용(유지)할지 묻는다(<261003_1.1>(7.3)),
        2 = 사용자가 고쳤지만 단계 개정판 번호가 달라 유지할 수 없다(새 목록 설치 + 바탕화면 사본).
  '사용자가 고쳤는지'는 Original 과 비교해 판단한다 — 예전처럼 Packaged 와 비교하면, 사용자가 손대지
  않았어도 새 버전의 목록이 바뀌기만 하면 고친 것으로 보였다. Original 이 없으면(그 기능이 생기기 전
  설치본 등) 예전 방식대로 Packaged 와 비교한다. }
function DecideWordsJson(Live, Packaged, Original: AnsiString; HasOriginal: Boolean): Integer;
var
  Modified: Boolean;
begin
  Result := 0;
  { <260927_2> 제시어 목록 형식이 바뀌었다(자리연습·오락 공용, "hangul"/"english" 묶음). 예전 형식의
    words.json 은 새 프로그램이 읽지 못해(내장 예비본 10개씩만 쓰게 됨) 유지할 의미가 없으므로,
    내용이 달라도 '유지할 것 없음'으로 보고 새 목록을 깐다(<261003_1.1>(7.6)). }
  if BytePos('"hangul"', Live) = 0 then Exit;
  if Live = Packaged then Exit; { 새 목록과 똑같으면 지킬 것이 없다 }

  if HasOriginal then
    Modified := (Live <> Original)
  else
    Modified := True; { 원본 사본이 없으면 예전 방식(이번 패키지와 다르면 고친 것으로 본다) }
  if not Modified then Exit; { 사용자가 고치지 않았으면 묻지 않고 새 목록을 설치한다 }

  if WordsStageVersion(Live) <> WordsStageVersion(Packaged) then
    Result := 2
  else
    Result := 1;
end;

{ <261003_1.1> (7.5) 안내의 단서: 설치하려는 새 단어 목록의 단계 개정판 번호가 사용자 PC 의 단어 목록보다
  낮은가(더 낮은 버전을 설치하는 경우). }
function PackagedStageVersionIsLower(Live, Packaged: AnsiString): Boolean;
begin
  Result := WordsStageVersion(Packaged) < WordsStageVersion(Live);
end;
