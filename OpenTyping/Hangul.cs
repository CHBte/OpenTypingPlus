namespace OpenTyping
{
    /// <summary>
    /// 한글 음절(Hangul Syllables, U+AC00 '가' ~ U+D7A3 '힣') 범위 판정을 한 곳에서 정의한다.
    /// </summary>
    internal static class Hangul
    {
        public const char FirstSyllable = (char)0xAC00;
        public const char LastSyllable = (char)0xD7A3;

        public static bool IsSyllable(char ch) => ch >= FirstSyllable && ch <= LastSyllable;
    }
}
