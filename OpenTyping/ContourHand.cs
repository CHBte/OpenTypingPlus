namespace OpenTyping
{
    /// <summary>
    /// 물리 키 → 담당 손가락 배정표 (<260718_10>). 어느 손(왼/오른)의 어느 손가락(0~3 = 새끼~검지)이
    /// 그 키를 누르는지만 알려 준다.
    ///
    /// <260927_8>: 예전에는 이 배정을 바탕으로 실측 손 윤곽(HandContourData)을 유사변환·합성 변형해
    /// 키별 손 모양을 직접 그렸으나(BuildLibrary/GetPosePts/Deform 등), 물리 키별 SVG(hands\*.svg)가
    /// 전부 갖춰지면서 그 그리기 코드는 이미 실행되지 않는 상태였다(모든 배정된 키에 SVG가 있어
    /// LoadOrFallback의 폴백이 호출될 일이 없었음). 그리기 코드를 걷어내고, SVG가 없을 때는
    /// FingerLayer 가 기본 자리 그림(home-left.svg/home-right.svg)을 대신 쓰도록 단순화했다.
    /// 이 배정표 자체(어느 키를 어느 손가락이 담당하는지)는 <see cref="FingerLayer"/>가 왼손/오른손
    /// SVG 파일 이름을 고르는 데 계속 쓰므로 그대로 남긴다.
    /// </summary>
    internal static class ContourHand
    {
        // (오른손 여부, 손가락 번호 0~3 = 새끼~검지).
        // 0행(숫자 행)은 [5](열 5)까지 왼손, 1~3행은 열 4까지 왼손 담당이다 (<260718_10>).
        private static readonly (bool IsRight, int Finger)[] FingerRow0 =
        {
            (false, 0), (false, 0), (false, 1), (false, 2), (false, 3), (false, 3),
            (true, 3), (true, 3), (true, 2), (true, 1), (true, 0), (true, 0), (true, 0),
        };
        private static readonly (bool IsRight, int Finger)[] FingerRow123 =
        {
            (false, 0), (false, 1), (false, 2), (false, 3), (false, 3),
            (true, 3), (true, 3), (true, 2), (true, 1), (true, 0), (true, 0), (true, 0),
        };

        /// <summary>이 물리 키(행·열)를 어느 손의 몇 번째 손가락이 담당하는지. 배정이 없으면 false
        /// (숫자 행 밖·기존 자판 격자를 벗어난 자리 등 — 그 경우 기본 자리 포즈를 쓴다).</summary>
        public static bool TryGetFinger(int row, int col, out bool isRight, out int finger)
        {
            isRight = false;
            finger = 0;
            if (row < 0 || row > 3) return false;

            (bool IsRight, int Finger)[] table = row == 0 ? FingerRow0 : FingerRow123;
            if (col < 0 || col >= table.Length) return false;

            isRight = table[col].IsRight;
            finger = table[col].Finger;
            return true;
        }
    }
}
