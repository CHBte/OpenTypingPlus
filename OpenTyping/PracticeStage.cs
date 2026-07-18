using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// '자리연습'의 연습 단계 하나. 연습 항목은 (키 위치, 윗글쇠 여부) 쌍으로,
    /// 적은 빈도/많은 빈도 두 그룹으로 나뉜다.
    /// 출력 원리는 "가중 복원추출 균등난수 방식": 많은 빈도 항목을 HighWeight표,
    /// 적은 빈도 항목을 1표 넣은 다중집합에서 매번 독립적으로 균등하게 뽑는다
    /// (연속 중복 허용, 윗글쇠 여부는 항목에 고정).
    /// </summary>
    public class PracticeStage
    {
        public const int HighWeight = 3; // 많은 빈도 : 적은 빈도 = 3 : 1

        public struct StageItem
        {
            public StageItem(int row, int column, bool isShift = false)
            {
                Pos = new KeyPos(row, column);
                IsShift = isShift;
            }

            public KeyPos Pos { get; }
            public bool IsShift { get; }
        }

        public PracticeStage(string name, IList<StageItem> lowItems, IList<StageItem> highItems)
        {
            Name = name;
            LowItems = lowItems;
            HighItems = highItems;
        }

        public string Name { get; }
        public IList<StageItem> LowItems { get; }   // 적은 빈도 (1표)
        public IList<StageItem> HighItems { get; }  // 많은 빈도 (HighWeight표)

        /// <summary>가중 다중집합(추출용 풀)을 만든다.</summary>
        public List<StageItem> BuildPool()
        {
            var pool = new List<StageItem>(LowItems);
            for (int i = 0; i < HighWeight; i++)
            {
                pool.AddRange(HighItems);
            }
            return pool;
        }
    }

    /// <summary>
    /// '두벌식 표준' 자판 전용 한글·특수 기호 연습 단계 정의 (1~13단계).
    /// 키 위치는 KeyLayoutData 격자 기준 (행, 열)이다.
    /// </summary>
    public static class DubeolsikStages
    {
        // 기본 자리(홈 포지션) 8키: ㅁㄴㅇㄹ / ㅓㅏㅣ;
        private static readonly PracticeStage.StageItem[] HomeLeft =
        {
            new PracticeStage.StageItem(2, 0), // ㅁ
            new PracticeStage.StageItem(2, 1), // ㄴ
            new PracticeStage.StageItem(2, 2), // ㅇ
            new PracticeStage.StageItem(2, 3), // ㄹ
        };
        private static readonly PracticeStage.StageItem[] HomeRight =
        {
            new PracticeStage.StageItem(2, 6), // ㅓ
            new PracticeStage.StageItem(2, 7), // ㅏ
            new PracticeStage.StageItem(2, 8), // ㅣ
            new PracticeStage.StageItem(2, 9), // ;
        };
        private static PracticeStage.StageItem[] HomeAll => HomeLeft.Concat(HomeRight).ToArray();

        // 숫자 키 1~0 (윗글쇠 아님)
        private static readonly PracticeStage.StageItem[] Numbers1To0 =
        {
            new PracticeStage.StageItem(0, 1), new PracticeStage.StageItem(0, 2),
            new PracticeStage.StageItem(0, 3), new PracticeStage.StageItem(0, 4),
            new PracticeStage.StageItem(0, 5), new PracticeStage.StageItem(0, 6),
            new PracticeStage.StageItem(0, 7), new PracticeStage.StageItem(0, 8),
            new PracticeStage.StageItem(0, 9), new PracticeStage.StageItem(0, 10),
        };

        public static readonly IList<PracticeStage> Stages = BuildStages();

        private static IList<PracticeStage> BuildStages()
        {
            var stages = new List<PracticeStage>
            {
                // 1단계(기본 자리): 빈도 구분 없음 → 전부 적은 빈도 그룹에 두어 균등하게
                new PracticeStage("1단계(기본 자리)",
                    HomeAll,
                    new PracticeStage.StageItem[0]),

                new PracticeStage("2단계(왼쪽 윗자리)",
                    HomeLeft,
                    new[]
                    {
                        new PracticeStage.StageItem(1, 0), // ㅂ
                        new PracticeStage.StageItem(1, 1), // ㅈ
                        new PracticeStage.StageItem(1, 2), // ㄷ
                        new PracticeStage.StageItem(1, 3), // ㄱ
                    }),

                new PracticeStage("3단계(오른쪽 윗자리)",
                    HomeRight,
                    new[]
                    {
                        new PracticeStage.StageItem(1, 6), // ㅕ
                        new PracticeStage.StageItem(1, 7), // ㅑ
                        new PracticeStage.StageItem(1, 8), // ㅐ
                        new PracticeStage.StageItem(1, 9), // ㅔ
                    }),

                new PracticeStage("4단계(왼쪽 아랫자리)",
                    HomeLeft,
                    new[]
                    {
                        new PracticeStage.StageItem(3, 0), // ㅋ
                        new PracticeStage.StageItem(3, 1), // ㅌ
                        new PracticeStage.StageItem(3, 2), // ㅊ
                        new PracticeStage.StageItem(3, 3), // ㅍ
                    }),

                new PracticeStage("5단계(오른쪽 아랫자리)",
                    HomeRight,
                    new[]
                    {
                        new PracticeStage.StageItem(3, 6), // ㅡ
                        new PracticeStage.StageItem(3, 7), // ,
                        new PracticeStage.StageItem(3, 8), // .
                        new PracticeStage.StageItem(3, 9), // /
                    }),

                new PracticeStage("6단계(가운데 왼쪽 자리)",
                    HomeLeft,
                    new[]
                    {
                        new PracticeStage.StageItem(1, 4), // ㅅ
                        new PracticeStage.StageItem(2, 4), // ㅎ
                        new PracticeStage.StageItem(3, 4), // ㅠ
                    }),

                new PracticeStage("7단계(가운데 오른쪽 자리)",
                    HomeRight,
                    new[]
                    {
                        new PracticeStage.StageItem(1, 5), // ㅛ
                        new PracticeStage.StageItem(2, 5), // ㅗ
                        new PracticeStage.StageItem(3, 5), // ㅜ
                    }),

                new PracticeStage("8단계(Shift 기본)",
                    HomeAll,
                    new[]
                    {
                        new PracticeStage.StageItem(1, 0, true), // ㅃ
                        new PracticeStage.StageItem(1, 1, true), // ㅉ
                        new PracticeStage.StageItem(1, 2, true), // ㄸ
                        new PracticeStage.StageItem(1, 3, true), // ㄲ
                        new PracticeStage.StageItem(1, 4, true), // ㅆ
                        new PracticeStage.StageItem(1, 8, true), // ㅒ
                        new PracticeStage.StageItem(1, 9, true), // ㅖ
                        new PracticeStage.StageItem(2, 9, true), // :
                        new PracticeStage.StageItem(3, 7, true), // <
                        new PracticeStage.StageItem(3, 8, true), // >
                        new PracticeStage.StageItem(3, 9, true), // ?
                    }),

                new PracticeStage("9단계(숫자 행 1)",
                    HomeAll,
                    new[]
                    {
                        new PracticeStage.StageItem(0, 1),  // 1
                        new PracticeStage.StageItem(0, 2),  // 2
                        new PracticeStage.StageItem(0, 3),  // 3
                        new PracticeStage.StageItem(0, 4),  // 4
                        new PracticeStage.StageItem(0, 7),  // 7
                        new PracticeStage.StageItem(0, 8),  // 8
                        new PracticeStage.StageItem(0, 9),  // 9
                        new PracticeStage.StageItem(0, 10), // 0
                    }),

                new PracticeStage("10단계(숫자 행 2)",
                    HomeAll.Concat(new[]
                    {
                        new PracticeStage.StageItem(0, 1),  // 1
                        new PracticeStage.StageItem(0, 2),  // 2
                        new PracticeStage.StageItem(0, 3),  // 3
                        new PracticeStage.StageItem(0, 4),  // 4
                        new PracticeStage.StageItem(0, 7),  // 7
                        new PracticeStage.StageItem(0, 8),  // 8
                        new PracticeStage.StageItem(0, 9),  // 9
                        new PracticeStage.StageItem(0, 10), // 0
                    }).ToArray(),
                    new[]
                    {
                        new PracticeStage.StageItem(0, 0, true), // ~
                        new PracticeStage.StageItem(0, 5),       // 5
                        new PracticeStage.StageItem(0, 6),       // 6
                        new PracticeStage.StageItem(0, 11),      // -
                        new PracticeStage.StageItem(0, 12),      // =
                    }),

                new PracticeStage("11단계(특수 기호 1)",
                    Numbers1To0,
                    new[]
                    {
                        new PracticeStage.StageItem(0, 9, true),   // (
                        new PracticeStage.StageItem(0, 10, true),  // )
                        new PracticeStage.StageItem(1, 10),        // [
                        new PracticeStage.StageItem(1, 10, true),  // {
                        new PracticeStage.StageItem(1, 11),        // ]
                        new PracticeStage.StageItem(1, 11, true),  // }
                        new PracticeStage.StageItem(2, 10),        // '
                        new PracticeStage.StageItem(2, 10, true),  // "
                    }),

                new PracticeStage("12단계(특수 기호 2)",
                    Numbers1To0.Concat(new[]
                    {
                        new PracticeStage.StageItem(0, 11), // -
                        new PracticeStage.StageItem(0, 12), // =
                    }).ToArray(),
                    new[]
                    {
                        new PracticeStage.StageItem(0, 1, true),  // !
                        new PracticeStage.StageItem(0, 2, true),  // @
                        new PracticeStage.StageItem(0, 5, true),  // %
                        new PracticeStage.StageItem(0, 11, true), // _
                        new PracticeStage.StageItem(0, 12, true), // +
                    }),
            };

            // 13단계(연습한 키 전체): 1~12단계 모든 항목의 합집합에서 균등 추출 (가중 없음)
            var union = stages.SelectMany(s => s.LowItems.Concat(s.HighItems))
                              .Distinct()
                              .ToList();
            stages.Add(new PracticeStage("13단계(연습한 키 전체)",
                union,
                new PracticeStage.StageItem[0]));

            return stages;
        }
    }
}
