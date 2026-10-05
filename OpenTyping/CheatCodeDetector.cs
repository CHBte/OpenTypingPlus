using System.Collections.Generic;
using System.Linq;
using WinKey = System.Windows.Input.Key;

namespace OpenTyping
{
    /// <summary>
    /// (<260724_2>(3), <산성비 타자 오락 260812_15>) "효범미남"/"gyqjaalska"/"GYQJAALSKA" 치트코드 감지.
    /// 자리연습('자리연습' 메뉴)과 산성비 오락이 각자 같은 물리 키 시퀀스·버퍼 로직을 따로
    /// 들고 있던 것을 한 곳으로 모았다. 물리 키(WinKey)로 보므로 한글 IME 여부·대소문자와
    /// 무관하게 잡힌다.
    /// </summary>
    internal sealed class CheatCodeDetector
    {
        private static readonly WinKey[] Sequence =
        {
            WinKey.G, WinKey.Y, WinKey.Q, WinKey.J, WinKey.A,
            WinKey.A, WinKey.L, WinKey.S, WinKey.K, WinKey.A,
        };

        private readonly List<WinKey> buffer = new List<WinKey>();

        /// <summary>
        /// 키 하나를 넣는다. 글자 키(A~Z)가 아니면 무시하고 false. 시퀀스가 방금 완성됐으면
        /// true를 돌려주고 버퍼를 스스로 비운다.
        /// </summary>
        public bool Feed(WinKey key)
        {
            if (key < WinKey.A || key > WinKey.Z) return false;

            buffer.Add(key);
            if (buffer.Count > Sequence.Length) buffer.RemoveAt(0);
            if (buffer.Count < Sequence.Length || !buffer.SequenceEqual(Sequence)) return false;

            buffer.Clear();
            return true;
        }
    }
}
