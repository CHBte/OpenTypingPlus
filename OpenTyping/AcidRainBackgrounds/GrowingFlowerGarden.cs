using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenTyping
{
    /// <summary>
    /// 배경 세트 '자라는 꽃밭'(acidrain-bg3 — 썰렁이 지시 <261024_1>, 이름은 <261025_3>(1)). 나무 2그루와 꽃 식물이 자라고 날벌레가 찾아오는
    /// 이야기: 1 나무 줄기와 가지 / 2 + 줄기 뒤 녹색 큰 원, 싹 / 3 + 연두 열매, 줄기와 잎 / 4 + 작은 나무 열매 빨강, 봉오리 / 5 + 큰 나무 열매
    /// 포도색, 꽃 / 6 + 벌 / 10 + 나비. 그림은 `기준 장면 + 개별 생성 방식`을 응용해 캔바 AI로 그린 '모음 그림'이다. 나무의 녹색 큰 원·작은
    /// 원(열매)과 날벌레가 지나간 길(선)은 코드로 그린다. 겹치는 차례(뒤 → 앞)는 나무, 날벌레, 꽃 식물이다(<261024_1>(3.2.3.2)·(4)).
    /// </summary>
    internal sealed class GrowingFlowerGarden : BackgroundSet
    {
        public const string SetId = "GrowingFlowerGarden";
        public override string Id => SetId;
        public override string Name => "자라는 꽃밭";

        public override IReadOnlyList<string> SheetFiles { get; } =
            new[] { "clouds", "sprouts", "stems", "flowers", "buds", "trees", "tree_small", "insects" };

        // 두벌식 표준 한글은 오락이 7단계(1~6·10단계 배경), QWERTY 영문은 6단계(1~5단계 같은 번호, 6단계(마지막)가 10단계 배경 —
        // <261024_1>(1)·(2)).
        private static readonly int[] KoreanStages = { 1, 2, 3, 4, 5, 6, 10 };
        private static readonly int[] EnglishStages = { 1, 2, 3, 4, 5, 10 };
        public override IReadOnlyList<int> Stages => KoreanStages;
        public override IReadOnlyList<int> StagesFor(string keyboardKey) => keyboardKey == "en" ? EnglishStages : null;

        // 구름(clouds.png 속 조각 → 게임 영역의 왼쪽·위·너비). 단계와 관계없이 늘 같은 자리이고, 셋의 높이를 서로 다르게 둔다(썰렁이 지시).
        internal static readonly (Int32Rect Piece, double Left, double Top, double Width)[] CloudSpots =
        {
            (new Int32Rect(179, 216, 143, 85),    66, 50,  56),
            (new Int32Rect(694, 214, 263, 88),   372, 92, 102),
            (new Int32Rect(1298, 198, 207, 103), 704, 30,  80),
        };
        public override IReadOnlyList<(Int32Rect Piece, double Left, double Top, double Width)> Clouds => CloudSpots;

        // 나무(<261024_1>(1)): 그림 파일, 줄기·가지 조각, 그림 속 줄기 가운데 x·가장 낮은 가지 y, 게임 영역의 줄기 x, 줄기 그림의 높이(px),
        // 익은 열매 색, 열매가 익는 단계, 열매 수, 녹색 큰 타원의 너비(기준 원 반지름의 몇 배). 작은 나무는 왼쪽, 큰 나무(꽃의 약 5배, 썰렁이
        // 검토)는 오른쪽이다. trees.png 의 왼쪽 나무는 쓰지 않는다(썰렁이 검토로 tree_small.png 로 바꿈). 큰 나무의 타원은 썰렁이 검토로 조금 좁다.
        internal static readonly (string File, Int32Rect Piece, double TrunkX, double LowBranchY, double GameX, double Height,
                                  Color Ripe, int RipeScene, int Fruits, double CrownWidth)[] Trees =
        {
            ("tree_small", new Int32Rect(282, 141, 519, 1189),  529.5, 682,  62, 150, Color.FromRgb(0xf0, 0x3e, 0x3e), 4,  7, 1.03),
            ("trees",      new Int32Rect(946, 176, 328, 823),  1117.5, 585, 815, 245, Color.FromRgb(0x70, 0x48, 0xe8), 5, 11, 0.95),
        };

        // 2단계의 싹(sprouts.png) 다섯 가지.
        private static readonly Int32Rect[] PlantSprouts =
        {
            new Int32Rect(101, 527, 151, 145), new Int32Rect(467, 500, 86, 172), new Int32Rect(764, 449, 136, 223),
            new Int32Rect(1138, 559, 56, 113), new Int32Rect(1430, 449, 126, 223),
        };

        // 3단계부터의 줄기와 잎(stems.png): 조각, 그림 속 줄기 x, 줄기 끝 y. 모두 곧은 줄기에 잎 2장이다. 그림의 왼쪽에서 3번째(떡잎처럼
        // 보이는 것)는 쓰지 않는다(썰렁이 검토).
        private static readonly (Int32Rect Piece, double StemX, double TopY)[] PlantStems =
        {
            (new Int32Rect(55, 466, 168, 325),    128.1, 466),
            (new Int32Rect(325, 308, 192, 482),   418.6, 331),
            (new Int32Rect(911, 307, 164, 484),   993.0, 307),
            (new Int32Rect(1163, 481, 186, 310), 1254.6, 481),
            (new Int32Rect(1428, 243, 159, 547), 1502.4, 243),
        };

        // 5단계의 꽃(flowers.png)과 4단계의 봉오리(buds.png): 같은 차례끼리 짝이다(봉오리가 같은 꽃으로 핀다). 조각, 그림 속 밑동 x, 밑동
        // 토막의 길이(줄). 밑동 토막은 줄기와 녹색이 조금 달라, 떼어 내고 꽃 아랫부분이 줄기 끝을 덮게 놓는다. 차례: 코스모스 분홍·보라·흰색,
        // 튤립 빨강·노랑·분홍, 백합 흰색·분홍, 데이지, 파란 들꽃.
        internal static readonly (Int32Rect Piece, double StubX, int StubLen)[] PlantFlowers =
        {
            (new Int32Rect(134, 170, 188, 200),  225.6, 16), (new Int32Rect(440, 170, 186, 200),  532.6, 18),
            (new Int32Rect(755, 170, 185, 200),  846.8, 19), (new Int32Rect(1108, 169, 140, 201), 1176.0, 27),
            (new Int32Rect(1407, 183, 128, 187), 1470.8, 24), (new Int32Rect(150, 588, 134, 187),  216.1, 24),
            (new Int32Rect(438, 565, 182, 210),  528.9, 34), (new Int32Rect(753, 580, 176, 195),  841.7, 33),
            (new Int32Rect(1083, 594, 166, 181), 1165.8, 26), (new Int32Rect(1417, 622, 111, 153), 1470.8, 39),
        };
        internal static readonly (Int32Rect Piece, double StubX, int StubLen)[] PlantBuds =
        {
            (new Int32Rect(186, 235, 52, 83),   211.7, 15), (new Int32Rect(493, 224, 60, 94),   522.9, 15),
            (new Int32Rect(817, 237, 49, 81),   840.7, 15), (new Int32Rect(1129, 205, 65, 113), 1160.9, 14),
            (new Int32Rect(1430, 216, 63, 102), 1459.9, 14), (new Int32Rect(182, 605, 60, 138),  211.8, 15),
            (new Int32Rect(493, 603, 60, 140),  522.8, 15), (new Int32Rect(812, 604, 58, 139),  840.5, 15),
            (new Int32Rect(1136, 655, 51, 88),  1160.6, 15), (new Int32Rect(1438, 674, 44, 69),  1459.8, 15),
        };

        // 꽃·봉오리의 흰 꽃잎에 입히는 색(PlantFlowers·PlantBuds 와 같은 차례, null 이면 그대로 — SceneKit.Tinted): 흰 코스모스·흰 백합은
        // 주황, 데이지는 보라(썰렁이 지시 2026-10-26, 태그 없는 지시 — 흰 하늘에서 잘 안 보여서).
        internal static readonly Color?[] HeadTints =
        {
            null, null, SceneKit.FlowerOrange, null, null, null, SceneKit.FlowerOrange, null, SceneKit.FlowerPurple, null,
        };

        // 꽃의 크기 배율(PlantFlowers 와 같은 차례, 1 이면 그대로): 파란 들꽃은 모음 그림에서 다른 꽃보다 작게 그려져(꽃 부분 약 105×112px,
        // 보라 코스모스는 180×180px) 줄기의 잎에 비해 작아 보여, 보라 코스모스만 하게 1.65배로 키운다(썰렁이 지시 2026-10-26, 태그 없는 지시).
        internal static readonly double[] FlowerSizes = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.65 };

        // 날벌레(insects.png, 모두 오른쪽을 본다): 벌 두 가지, 나비 두 가지(분홍·노랑). 조각과 그림 속 배 끝(벌은 침 끝 — 가장 왼쪽의 어두운
        // 픽셀, 나비는 몸통의 가장 아래 갈색 픽셀로 잼). 지나온 길 선은 여기서 시작한다(썰렁이 지시). 분홍 나비는 배 끝이 왼쪽 아래, 노란
        // 나비는 오른쪽 아래다.
        private static readonly (Int32Rect Piece, double TipX, double TipY)[] BeePieces =
        {
            (new Int32Rect(217, 407, 124, 128), 225, 515.2), (new Int32Rect(584, 421, 126, 117), 594, 515.2),
        };
        private static readonly (Int32Rect Piece, double TipX, double TipY)[] ButterflyPieces =
        {
            (new Int32Rect(961, 409, 149, 151), 1011.0, 529), (new Int32Rect(1321, 418, 140, 138), 1416.7, 523),
        };

        // 날벌레의 자리(<261024_1>(1) 6·10단계): 그림, 게임 영역의 가운데 x, 땅선에서 밑변까지의 높이, 오른쪽을 보는가, 지나온 길 선이
        // 오른쪽에 있는가, 선의 길이. 길 선은 대개 보는 방향의 반대쪽(뒤)이지만, 가운데 나비는 썰렁이 검토로 그림만 뒤집고 선은 오른쪽에 둔다.
        // 벌끼리·나비끼리 높이를 서로 다르게 둔다(썰렁이 지시). 꽃 위나 꽃 사이에 두어 꽃(앞 레이어)에 덜 가린다(<261024_1>(4)).
        internal static readonly (int Piece, double X, double Bottom, bool Right, bool TrailRight, double Trail)[] Bees =
        {
            (0, 214, 70, true, false, 44), (1, 392, 96, false, true, 38), (0, 548, 79, false, true, 50), (1, 676, 104, true, false, 36),
        };
        internal static readonly (int Piece, double X, double Bottom, bool Right, bool TrailRight, double Trail)[] Butterflies =
        {
            (0, 300, 112, true, false, 46), (1, 468, 86, true, true, 40), (0, 612, 128, true, false, 52),
        };

        private const double SproutScale = 0.075;    // 싹: 모음 그림 1px → 게임 영역 px
        private const double StemScale = 0.088;      // 줄기와 잎
        private const double HeadScale = 1.2;        // 꽃·봉오리는 줄기보다 이만큼 크게
        private const double HeadOverlap = 2.2;      // 꽃·봉오리 아래 끝이 줄기 끝보다 이만큼(px) 아래로 내려와 줄기 끝을 덮는다
        private const double BeeHeight = 16, ButterflyHeight = 22;

        private static readonly Color UnripeColor = Color.FromRgb(0xa9, 0xd6, 0x4b);   // 덜 익은 열매(연두, 썰렁이 검토로 조금 어둡게)

        // 썰렁이 검토로 손으로 옮긴 열매: 나무, 열매 차례, 새 자리(기준 원 가운데에서 잰 x·y ÷ 반지름). 난수로 정한 자리 대신 쓴다.
        // 모든 단계가 같은 자리를 쓰므로 3단계의 연두 열매도 함께 옮겨진다.
        private static readonly (int Tree, int Index, double X, double Y)[] FruitMoves =
        {
            (0, 0, -0.605, 0.108),   // 작은 나무: 가운데 세 열매 중 위에서 두 번째 → 왼쪽 두 열매 사이에 조금 어긋나게(2026-10-25)
        };

        private sealed class PlantSpot
        {
            public double X;                         // 게임 영역의 가운데 x
            public int Sprout, Stem, Head;           // 싹·줄기와 잎·꽃(봉오리)의 차례
            public bool SproutFlip, StemFlip, HeadFlip;
            public double SproutSize, StemSize;      // 크기 배율
        }

        private static List<PlantSpot> plantSpots;

        /// <summary>
        /// 꽃 식물의 자리와 모양(<261024_1>(1) 2단계). 몇 가지 그림을 규칙이 보이지 않는 순서로 흩어 촘촘히 놓는다 — 바로 앞(싹·꽃은
        /// 앞의 둘)과 같은 그림이 이어지지 않게 고르고, 간격·크기·좌우 방향도 조금씩 다르게 한다. 씨앗이 정해진 난수라 늘 같은 배치가
        /// 나오므로 단계가 바뀌어도 같은 자리에 선다. 나무 줄기 둘레는 비워 둔다(<261024_1>(4)).
        /// </summary>
        private static List<PlantSpot> PlantSpots
        {
            get
            {
                if (plantSpots != null) return plantSpots;
                var rnd = new Random(261024);
                int Pick(int n, int avoid1, int avoid2)
                {
                    int v;
                    do v = rnd.Next(n); while (v == avoid1 || v == avoid2);
                    return v;
                }
                var spots = new List<PlantSpot>();
                double x = 9;
                while (x < 893)
                {
                    if (Trees.Any(t => Math.Abs(x - t.GameX) < 16))
                    {
                        x += 4;
                        continue;
                    }
                    PlantSpot prev = spots.Count > 0 ? spots[spots.Count - 1] : null;
                    PlantSpot prev2 = spots.Count > 1 ? spots[spots.Count - 2] : null;
                    spots.Add(new PlantSpot
                    {
                        X = x,
                        Sprout = Pick(PlantSprouts.Length, prev?.Sprout ?? -1, prev2?.Sprout ?? -1),
                        Stem = Pick(PlantStems.Length, prev?.Stem ?? -1, -1),
                        Head = Pick(PlantFlowers.Length, prev?.Head ?? -1, prev2?.Head ?? -1),
                        SproutFlip = rnd.Next(2) == 0,
                        StemFlip = rnd.Next(2) == 0,
                        HeadFlip = rnd.Next(2) == 0,
                        SproutSize = 0.85 + rnd.NextDouble() * 0.3,
                        StemSize = 0.88 + rnd.NextDouble() * 0.24,
                    });
                    x += 21 + rnd.NextDouble() * 10;
                }
                plantSpots = spots;
                return spots;
            }
        }

        public override void Build(ISceneCanvas canvas, int scene)
        {
            for (int i = 0; i < Trees.Length; i++)
            {
                (Canvas unit, double anchor) = BuildTree(i, scene);
                canvas.Add(unit, anchor, Trees[i].GameX, 0, RiseKind.Ground);
            }
            if (scene >= 6)
            {
                foreach (var b in Bees)
                {
                    var bp = BeePieces[b.Piece];
                    (Canvas unit, double anchor) = SceneKit.Flyer(Piece("insects", bp.Piece), bp.TipX, bp.TipY, 0, BeeHeight, b.Right, b.TrailRight, b.Trail);
                    canvas.Add(unit, anchor, b.X, b.Bottom, RiseKind.Flyer);
                }
            }
            if (scene >= 10)
            {
                foreach (var b in Butterflies)
                {
                    var fp = ButterflyPieces[b.Piece];
                    (Canvas unit, double anchor) = SceneKit.Flyer(Piece("insects", fp.Piece), fp.TipX, fp.TipY, 0, ButterflyHeight, b.Right, b.TrailRight, b.Trail);
                    canvas.Add(unit, anchor, b.X, b.Bottom, RiseKind.Party);
                }
            }
            if (scene >= 2)
            {
                foreach (PlantSpot spot in PlantSpots)
                {
                    (Canvas unit, double anchor) = BuildPlant(spot, scene);
                    canvas.Add(unit, anchor, spot.X, 0, RiseKind.Ground);
                }
            }
        }

        /// <summary>
        /// 나무 한 그루(<261024_1>(1)): 1단계 줄기와 가지, 2단계부터 줄기 뒤의 녹색 큰 원, 3단계부터 원 안의 연두색 작은 원(덜 익은 열매)이
        /// 겹친다. 작은 원은 RipeScene 단계부터 익은 색(작은 나무 빨강, 큰 나무 포도색)이다. 겹치는 차례는 녹색 큰 원 → 줄기 → 작은 원이다
        /// (<261024_1>(4)). 반환값의 anchor 는 그림 안의 줄기 가운데 x.
        /// </summary>
        private (Canvas Unit, double Anchor) BuildTree(int index, int scene)
        {
            var t = Trees[index];
            ScenePiece p = Piece(t.File, t.Piece);
            (Canvas unit, double anchor, TreeFrame f) =
                SceneKit.Tree(p, p, t.TrunkX, t.LowBranchY, t.Height, t.CrownWidth, scene >= 2 ? SceneKit.CrownBrush : null);
            if (unit != null && scene >= 3)
            {
                Brush fill = SceneKit.Frozen(scene >= t.RipeScene ? t.Ripe : UnripeColor);
                foreach ((double fx, double fy, double fr) in FruitSpots(index, t.Fruits, f.R))
                {
                    var fruit = new Ellipse { Width = 2 * fr, Height = 2 * fr, Fill = fill };
                    Canvas.SetLeft(fruit, f.OX + f.CX + fx - fr);
                    Canvas.SetTop(fruit, f.OY + f.CY + fy - fr);
                    unit.Children.Add(fruit);
                }
            }
            return (unit, anchor);
        }

        /// <summary>
        /// 기준 원(반지름 r, 녹색 큰 원 타원 안에 들어간다) 안의 열매 자리와 크기(원 가운데 기준). 크기와 자리를 고르게 흩어 좌우 대칭이 되지 않게 하고(썰렁이 지시),
        /// 서로 겹치지 않게 한다. 나무마다 씨앗이 정해진 난수라 오락을 다시 켜도 늘 같은 자리다. FruitMoves 의 열매만 손으로 정한 자리로 옮긴다.
        /// </summary>
        private static List<(double X, double Y, double R)> FruitSpots(int tree, int count, double r)
        {
            var rnd = new Random(2610240 + tree);
            var list = new List<(double X, double Y, double R)>();
            for (int tries = 0; list.Count < count && tries < 2000; tries++)
            {
                double fr = r * (0.075 + rnd.NextDouble() * 0.03);
                double a = rnd.NextDouble() * Math.PI * 2, d = Math.Sqrt(rnd.NextDouble()) * (r * 0.86 - fr);
                double fx = Math.Cos(a) * d, fy = Math.Sin(a) * d;
                if (list.Any(o => Math.Sqrt((o.X - fx) * (o.X - fx) + (o.Y - fy) * (o.Y - fy)) < o.R + fr + r * 0.04)) continue;
                list.Add((fx, fy, fr));
            }
            foreach (var mv in FruitMoves)
            {
                if (mv.Tree == tree && mv.Index < list.Count) list[mv.Index] = (mv.X * r, mv.Y * r, list[mv.Index].R);
            }
            return list;
        }

        /// <summary>
        /// 꽃 식물 한 포기: 2단계는 싹, 3단계부터는 줄기와 잎, 4단계는 그 꼭대기에 봉오리, 5단계부터는 꽃(<261024_1>(1)). 줄기와 잎은
        /// 3단계부터 같은 그림이고 봉오리·꽃만 코드로 겹친다(<261024_1>(0.1)). 반환값의 anchor 는 줄기(싹은 그림 가운데) x.
        /// </summary>
        private (Canvas Unit, double Anchor) BuildPlant(PlantSpot spot, int scene)
        {
            if (scene == 2)
            {
                ScenePiece sp = Piece("sprouts", PlantSprouts[spot.Sprout]);
                if (sp == null) return (null, 0);
                Image img = SceneKit.PieceImage(sp, SproutScale * spot.SproutSize, spot.SproutFlip);
                var sproutUnit = new Canvas { Width = img.Width, Height = img.Height };
                sproutUnit.Children.Add(img);
                return (sproutUnit, img.Width / 2);
            }

            var st = PlantStems[spot.Stem];
            ScenePiece head = null;
            double stubX = 0, headScale = HeadScale;
            if (scene >= 4)
            {
                var hd = scene >= 5 ? PlantFlowers[spot.Head] : PlantBuds[spot.Head];
                head = SceneKit.Tinted(Piece(scene >= 5 ? "flowers" : "buds", hd.Piece, hd.StubLen), HeadTints[spot.Head]);
                stubX = hd.StubX;
                if (scene >= 5) headScale *= FlowerSizes[spot.Head];
            }
            return SceneKit.StemWithHead(Piece("stems", st.Piece), st.StemX, st.TopY, spot.StemFlip, StemScale * spot.StemSize,
                                         head, stubX, spot.HeadFlip, headScale, HeadOverlap);
        }
    }
}
