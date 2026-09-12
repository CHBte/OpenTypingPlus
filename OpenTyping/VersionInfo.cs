using System.Reflection;

namespace OpenTyping
{
    /// <summary>
    /// 어셈블리의 실제 버전(AssemblyVersion, .NET 방식 네 자리 Major.Minor.Build.Revision)을
    /// 창 제목·'프로그램 정보' 창에 표시할 문자열로 만든다 (<2600912_5-1>).
    /// 예전엔 DisplayVersion(세 자리, csproj에 따로 손으로 적어 둔 값)을 따로 읽어 표시했는데,
    /// AssemblyVersion을 올려도 그쪽엔 반영이 안 돼 화면 표시가 따로 놀았다. 이제 AssemblyVersion을
    /// 유일한 출처로 삼아, 버전을 올리면(csproj 한 곳만 고치면) 모든 표시가 자동으로 맞는다.
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>"0.5.0.0"처럼 네 자리 그대로.</summary>
        public static readonly string Version4 =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(4) ?? "0.0.0.0";

        /// <summary>창 제목에 프로그램 이름 뒤에 붙이는 짧은 표기. 예: "열린타자+ v0.5.0.0".</summary>
        public static readonly string WindowTitle = "열린타자+ v" + Version4;
    }
}
