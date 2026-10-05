// <261003_1>(4.3) 단어 필터 도구(apply-word-filter.ps1)가 쓰는 단어 목록 json 해석·수정기. 단계·수준의
// "words" 배열만 찾아 그 안의 항목을 빼고, 파일의 나머지 모양(들여쓰기·줄바꿈·순서)은 그대로 둔다.
// PowerShell 5.1 의 Add-Type(옛 C# 5 컴파일러)이 OpenTyping\FilterWords.cs 와 함께 컴파일하므로 그에 맞는 문법만 쓴다.
// 앱 프로젝트(OpenTyping.csproj)에는 들어가지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class WordListEditor
{
    private sealed class Node
    {
        public char Kind;           // o 객체, a 배열, s 문자열, n 그 밖(수·true·false·null)
        public int Start, End;      // 원문에서의 범위 [Start, End)
        public string Str;          // 문자열 값
        public string Raw;          // 그 밖 값의 원문
        public List<string> Keys = new List<string>();
        public List<Node> Items = new List<Node>();
        public Node Get(string key)
        {
            for (int i = 0; i < Keys.Count; i++) if (Keys[i] == key) return Items[i];
            return null;
        }
    }

    private sealed class P
    {
        private readonly string t; private int i;
        public P(string text) { t = text; i = 0; }
        private void Ws() { while (i < t.Length && (t[i] == ' ' || t[i] == '\t' || t[i] == '\r' || t[i] == '\n' || t[i] == '﻿')) i++; }
        private Exception Err(string m) { return new FormatException("json 해석 실패(" + i + "번째 글자): " + m); }
        public Node Root()
        {
            Ws(); Node n = Value(); Ws();
            if (i != t.Length) throw Err("값 뒤에 남은 글자가 있습니다");
            return n;
        }
        private Node Value()
        {
            Ws();
            if (i >= t.Length) throw Err("값이 없습니다");
            char c = t[i];
            if (c == '{') return Obj();
            if (c == '[') return Arr();
            if (c == '"') { var s = new Node(); s.Kind = 's'; s.Start = i; s.Str = Str(); s.End = i; return s; }
            var n = new Node(); n.Kind = 'n'; n.Start = i;
            while (i < t.Length && ",}] \t\r\n".IndexOf(t[i]) < 0) i++;
            n.End = i; n.Raw = t.Substring(n.Start, n.End - n.Start);
            if (n.Raw.Length == 0) throw Err("값을 읽을 수 없습니다");
            return n;
        }
        private Node Obj()
        {
            var n = new Node(); n.Kind = 'o'; n.Start = i; i++;
            Ws();
            if (i < t.Length && t[i] == '}') { i++; n.End = i; return n; }
            while (true)
            {
                Ws(); if (i >= t.Length || t[i] != '"') throw Err("객체 이름이 필요합니다");
                string k = Str(); Ws();
                if (i >= t.Length || t[i] != ':') throw Err("':'가 필요합니다");
                i++;
                n.Keys.Add(k); n.Items.Add(Value()); Ws();
                if (i < t.Length && t[i] == ',') { i++; continue; }
                if (i < t.Length && t[i] == '}') { i++; n.End = i; return n; }
                throw Err("',' 또는 '}'가 필요합니다");
            }
        }
        private Node Arr()
        {
            var n = new Node(); n.Kind = 'a'; n.Start = i; i++;
            Ws();
            if (i < t.Length && t[i] == ']') { i++; n.End = i; return n; }
            while (true)
            {
                n.Items.Add(Value()); Ws();
                if (i < t.Length && t[i] == ',') { i++; continue; }
                if (i < t.Length && t[i] == ']') { i++; n.End = i; return n; }
                throw Err("',' 또는 ']'가 필요합니다");
            }
        }
        private string Str()
        {
            var sb = new StringBuilder(); i++;
            while (true)
            {
                if (i >= t.Length) throw Err("문자열이 닫히지 않았습니다");
                char c = t[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= t.Length) throw Err("잘못된 이스케이프");
                char e = t[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > t.Length) throw Err("잘못된 \\u 이스케이프");
                        sb.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4; break;
                    default: throw Err("잘못된 이스케이프");
                }
            }
        }
    }

    private sealed class WordArray
    {
        public string Where;        // "hangul 3단계 2수준" 꼴
        public Node Array;
    }

    private static List<WordArray> FindWordArrays(Node root)
    {
        var list = new List<WordArray>();
        if (root.Kind != 'o') return list;
        for (int s = 0; s < root.Keys.Count; s++)
        {
            Node sec = root.Items[s];
            if (sec.Kind != 'o') continue;
            Node stages = sec.Get("stages");
            if (stages == null || stages.Kind != 'a') continue;
            foreach (Node st in stages.Items)
            {
                if (st.Kind != 'o') continue;
                Node stNo = st.Get("stage"); Node levels = st.Get("levels");
                if (levels == null || levels.Kind != 'a') continue;
                foreach (Node lv in levels.Items)
                {
                    if (lv.Kind != 'o') continue;
                    Node lvNo = lv.Get("level"); Node words = lv.Get("words");
                    if (words == null || words.Kind != 'a') continue;
                    var wa = new WordArray();
                    wa.Where = root.Keys[s] + " " + (stNo != null ? stNo.Raw : "?") + "단계 " + (lvNo != null ? lvNo.Raw : "?") + "수준";
                    wa.Array = words;
                    list.Add(wa);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// keys(비교 열쇠 = OpenTyping.FilterWords.WordKey)와 같은 단어를 "words" 배열에서 뺀 새 원문을 돌려준다.
    /// report 에는 "어디: 지운 단어들", warnings 에는 지운 결과 빈 배열이 된 곳을 담는다. 지운 것이 없으면 원문 그대로.
    /// </summary>
    public static string RemoveWords(string text, ICollection<string> keys, List<string> report, List<string> warnings, out int removed)
    {
        removed = 0;
        Node root = new P(text).Root();
        var edits = new List<KeyValuePair<Node, string>>();   // (배열, 새 배열 원문)
        foreach (WordArray wa in FindWordArrays(root))
        {
            Node arr = wa.Array;
            var kept = new List<Node>();
            var gone = new List<string>();
            foreach (Node it in arr.Items)
            {
                if (it.Kind == 's' && keys.Contains(OpenTyping.FilterWords.WordKey(it.Str))) gone.Add(it.Str);
                else kept.Add(it);
            }
            if (gone.Count == 0) continue;
            removed += gone.Count;
            report.Add(wa.Where + ": " + gone.Count + "개 — " + string.Join(", ", gone.ToArray()));
            if (kept.Count == 0) warnings.Add(wa.Where + "의 단어가 하나도 남지 않았습니다.");

            string rebuilt;
            if (kept.Count == 0) rebuilt = "[]";
            else
            {
                Node first = arr.Items[0], last = arr.Items[arr.Items.Count - 1];
                string prefix = text.Substring(arr.Start + 1, first.Start - (arr.Start + 1));   // '[' 뒤 ~ 첫 항목 앞
                string suffix = text.Substring(last.End, (arr.End - 1) - last.End);            // 끝 항목 뒤 ~ ']' 앞
                string sep = arr.Items.Count >= 2
                    ? text.Substring(arr.Items[0].End, arr.Items[1].Start - arr.Items[0].End)  // 원래 쓰던 항목 사이 모양
                    : ", ";
                var sb = new StringBuilder("[");
                sb.Append(prefix);
                for (int k = 0; k < kept.Count; k++)
                {
                    if (k > 0) sb.Append(sep);
                    sb.Append(text, kept[k].Start, kept[k].End - kept[k].Start);
                }
                sb.Append(suffix).Append(']');
                rebuilt = sb.ToString();
            }
            edits.Add(new KeyValuePair<Node, string>(arr, rebuilt));
        }
        if (edits.Count == 0) return text;

        edits.Sort(delegate(KeyValuePair<Node, string> a, KeyValuePair<Node, string> b) { return b.Key.Start.CompareTo(a.Key.Start); });
        var result = new StringBuilder(text);
        foreach (var e in edits)
        {
            result.Remove(e.Key.Start, e.Key.End - e.Key.Start);
            result.Insert(e.Key.Start, e.Value);
        }
        string newText = result.ToString();
        new P(newText).Root();   // 고친 결과도 올바른 json 인지 다시 해석해 확인
        return newText;
    }

    /// <summary>원문의 "words" 배열들에 keys 와 같은 단어가 남아 있으면 그 단어들을 돌려준다(확인용).</summary>
    public static List<string> Remaining(string text, ICollection<string> keys)
    {
        var left = new List<string>();
        foreach (WordArray wa in FindWordArrays(new P(text).Root()))
            foreach (Node it in wa.Array.Items)
                if (it.Kind == 's' && keys.Contains(OpenTyping.FilterWords.WordKey(it.Str))) left.Add(wa.Where + " " + it.Str);
        return left;
    }
}
