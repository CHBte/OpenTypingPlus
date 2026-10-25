using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenTyping
{
    /// <summary>
    /// 배경 세트 '친환경 에너지 마을로'(acidrain-bg4 — 썰렁이 지시 2026-10-25 태그 없는 지시, 이름은 <261025_3>(1)). 오염된 공장 마을이
    /// 되살아나는 이야기: 1 연기를 뿜는 공장, 잿빛 집과 잎 없는 잿빛 나무, 짙은 매연 구름 → 2 연기가 멈추고 지붕에 태양광 판 → 3 풍력 발전기
    /// → 4 묘목을 심고 집·나무가 색을 되찾음 → 5 나무에 잎, 꽃밭, 자전거, 흰 구름 → 6 새들이 돌아옴 → 10 마을 잔치. 그림은 `기준 장면 +
    /// 개별 생성 방식`을 응용해 캔바 AI로 그린 '모음 그림'이다(나무 줄기·꽃·구름은 '자라는 꽃밭'과 같은 그림). 태양광 판, 나무의 잎(녹색
    /// 타원), 잔치의 삼각 깃발, 새가 지나간 길(선)은 코드로 그린다. 겹치는 차례(뒤 → 앞)는 연기, 풍력 발전기, 새, 나무, 집·공장, 잔치의
    /// 깃발과 사람, 자전거, 묘목, 꽃밭이다.
    /// </summary>
    internal sealed class EcoEnergyVillage : BackgroundSet
    {
        public const string SetId = "EcoEnergyVillage";
        public override string Id => SetId;
        public override string Name => "친환경 에너지 마을로";

        public override IReadOnlyList<string> SheetFiles { get; } = new[]
        {
            "clouds", "trees", "tree_small", "stems", "flowers", "buildings", "smoke", "turbine", "saplings_bikes", "birds", "people",
        };

        // 두벌식 표준 한글은 오락이 7단계(1~6·10단계 배경), QWERTY 영문은 6단계(1~5단계 같은 번호, 6단계(마지막)가 10단계 배경).
        private static readonly int[] KoreanStages = { 1, 2, 3, 4, 5, 6, 10 };
        private static readonly int[] EnglishStages = { 1, 2, 3, 4, 5, 10 };
        public override IReadOnlyList<int> Stages => KoreanStages;
        public override IReadOnlyList<int> StagesFor(string keyboardKey) => keyboardKey == "en" ? EnglishStages : null;

        // 구름(clouds.png 속 조각 → 게임 영역의 왼쪽·위·너비, 5단계부터의 흰 구름 기준). 늘 같은 자리이고 셋의 높이가 서로 다르다.
        // 단계마다 가운데를 그대로 둔 채 크기·밝기만 바뀐다(CloudLook).
        internal static readonly (Int32Rect Piece, double Left, double Top, double Width)[] CloudSpots =
        {
            (new Int32Rect(179, 216, 143, 85),    66, 50,  56),
            (new Int32Rect(694, 214, 263, 88),   372, 92, 102),
            (new Int32Rect(1298, 198, 207, 103), 704, 30,  80),
        };
        public override IReadOnlyList<(Int32Rect Piece, double Left, double Top, double Width)> Clouds => CloudSpots;

        /// <summary>
        /// 단계별 구름(Scale = 크기 배율, Shade = 흰 구름에 곱하는 밝기). 1단계는 기준 장면처럼 크고 짙은 회색 매연 구름이고(썰렁이 지시 —
        /// 색은 기준 장면 그대로 113,113,114), 단계마다 작아지고 밝아져 5단계부터 원래 흰 구름이다.
        /// </summary>
        public override (double Scale, double Shade) CloudLook(int scene)
        {
            switch (scene)
            {
                case 1: return (2.6, 113 / 255.0);
                case 2: return (2.1, 0.58);
                case 3: return (1.65, 0.72);
                case 4: return (1.3, 0.86);
                default: return (1.0, 1.0);
            }
        }

        // 집과 공장(buildings.png): 조각, 게임 영역의 가운데 x, 높이(px), 지붕면 안의 태양광 판 자리(그림 속 사각형), 지붕면 옆선의
        // 기울기(아래로 1px 갈 때 가로로 몇 px — 집은 왼쪽(-), 공장은 오른쪽(+). 그림에서 잰 값), 1~3단계에 잿빛인가.
        // 태양광 판은 2단계부터 코드로 얹되, 지붕면이 평행사변형이라 판도 그 옆선만큼 기울인다(썰렁이 지시 2026-10-25, 태그 없는 지시).
        // 집은 4단계부터 색을 되찾는다(공장의 벽돌색은 그대로).
        private static readonly (Int32Rect Piece, double GameX, double Height, Int32Rect Panel, double PanelSlant, bool Grays)[] TownBuildings =
        {
            (new Int32Rect(69, 355, 366, 321),    55,  66, new Int32Rect(135, 385, 150, 82), -0.529, true),
            (new Int32Rect(650, 355, 372, 321),  142,  62, new Int32Rect(716, 381, 156, 85), -0.511, true),
            (new Int32Rect(1222, 224, 399, 452), 300, 112, new Int32Rect(1340, 452, 220, 73), 0.577, false),
        };
        private const int FactoryIndex = 2;

        // 1단계에만 나오는 연기(smoke.png): 조각, 그림 속 밑동 가운데 x, 게임 화면의 연기 높이, 좌우 뒤집기, 그 연기가 나오는 공장 굴뚝 끝
        // (buildings.png 속 가운데 x, 위 y). 크기는 기준 장면처럼 크다(썰렁이 지시). 연기는 공장 뒤에 깔려 밑동이 굴뚝 끝에 가려진다.
        internal static readonly (Int32Rect Piece, double BottomX, double Height, bool Flip, double ChimneyX, double ChimneyY)[] SmokePlumes =
        {
            (new Int32Rect(42, 36, 468, 1374), 237.4, 380, false, 1283.5, 226),
            (new Int32Rect(588, 36, 461, 1374), 776.0, 330, true, 1364.5, 226),
        };

        // 나무('자라는 꽃밭'의 줄기 그림): 그림 파일, 줄기·가지 조각, 그림 속 줄기 가운데 x·가장 낮은 가지 y, 게임 영역의 줄기 x, 줄기 그림의
        // 높이, 잎(녹색 타원)의 너비(기준 원 반지름의 몇 배). 1~3단계는 잿빛 줄기, 4단계부터 갈색, 5단계부터 잎이 무성하다.
        private static readonly (string File, Int32Rect Piece, double TrunkX, double LowBranchY, double GameX, double Height, double CrownWidth)[] Trees =
        {
            ("tree_small", new Int32Rect(282, 141, 519, 1189),  529.5, 682, 203, 118, 1.03),
            ("trees",      new Int32Rect(946, 176, 328, 823),  1117.5, 585, 585, 165, 0.95),
        };

        // 풍력 발전기(turbine.png, 3단계부터): 조각, 게임 영역의 가운데 x, 높이. 한 그림을 높이만 달리해 두 기로 쓴다. 흰색이라 밝기를 조금 낮춘다.
        private const double TurbineShade = 0.8;
        private static readonly (Int32Rect Piece, double GameX, double Height)[] Turbines =
        {
            (new Int32Rect(166, 68, 693, 1410), 402, 200),
            (new Int32Rect(166, 68, 693, 1410), 484, 165),
        };

        // 묘목(4단계부터)과 자전거(5단계부터)(saplings_bikes.png): 조각, 게임 영역의 가운데 x, 높이, 좌우 뒤집기. 묘목은 공장 양옆 앞마당.
        private static readonly (Int32Rect Piece, double GameX, double Height, bool Flip)[] Saplings =
        {
            (new Int32Rect(139, 273, 107, 415), 238, 44, false),
            (new Int32Rect(498, 272, 97, 416),  362, 40, true),
        };
        private static readonly (Int32Rect Piece, double GameX, double Height, bool Flip)[] Bikes =
        {
            (new Int32Rect(747, 456, 360, 235),  643, 27, false),
            (new Int32Rect(1197, 450, 368, 241), 690, 28, false),
        };

        // 꽃밭(5단계부터, 풍력 발전기 앞): '자라는 꽃밭'의 줄기와 잎(stems.png)·꽃(flowers.png). 조각, 그림 속 줄기 x·줄기 끝 y / 밑동 x·밑동
        // 토막 길이. 왼쪽에서 3번째 줄기(떡잎 모양)와 종 모양 꽃은 '자라는 꽃밭' 때처럼 쓰지 않는다.
        private static readonly (Int32Rect Piece, double StemX, double TopY)[] PlantStems =
        {
            (new Int32Rect(55, 466, 168, 325),    128.1, 466),
            (new Int32Rect(325, 308, 192, 482),   418.6, 331),
            (new Int32Rect(911, 307, 164, 484),   993.0, 307),
            (new Int32Rect(1163, 481, 186, 310), 1254.6, 481),
            (new Int32Rect(1428, 243, 159, 547), 1502.4, 243),
        };
        internal static readonly (Int32Rect Piece, double StubX, int StubLen)[] PlantFlowers =
        {
            (new Int32Rect(134, 170, 188, 200),  225.6, 16), (new Int32Rect(440, 170, 186, 200),  532.6, 18),
            (new Int32Rect(755, 170, 185, 200),  846.8, 19), (new Int32Rect(1108, 169, 140, 201), 1176.0, 27),
            (new Int32Rect(1407, 183, 128, 187), 1470.8, 24), (new Int32Rect(150, 588, 134, 187),  216.1, 24),
            (new Int32Rect(438, 565, 182, 210),  528.9, 34), (new Int32Rect(753, 580, 176, 195),  841.7, 33),
            (new Int32Rect(1083, 594, 166, 181), 1165.8, 26), (new Int32Rect(1417, 622, 111, 153), 1470.8, 39),
        };
        // 꽃의 흰 꽃잎에 입히는 색(PlantFlowers 와 같은 차례, null 이면 그대로 — SceneKit.Tinted): '자라는 꽃밭'과 같다(흰 코스모스·흰
        // 백합은 주황, 데이지는 보라 — 썰렁이 지시 2026-10-26, 태그 없는 지시).
        internal static readonly Color?[] HeadTints =
        {
            null, null, SceneKit.FlowerOrange, null, null, null, SceneKit.FlowerOrange, null, SceneKit.FlowerPurple, null,
        };
        // 꽃의 크기 배율(PlantFlowers 와 같은 차례): '자라는 꽃밭'과 같다(파란 들꽃만 보라 코스모스만 하게 1.65배 — 썰렁이 지시 2026-10-26,
        // 태그 없는 지시).
        internal static readonly double[] FlowerSizes = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.65 };
        private const double FlowerBedLeft = 372, FlowerBedRight = 520;

        // 새(birds.png, 6단계부터, 모두 오른쪽을 본다): 조각과 그림 속 꼬리 끝(가장 왼쪽 픽셀). 지나간 길 선은 꼬리 끝에서 시작한다.
        private static readonly (Int32Rect Piece, double TipX, double TipY)[] BirdPieces =
        {
            (new Int32Rect(253, 367, 220, 166), 256, 495.0),
            (new Int32Rect(732, 426, 220, 106), 734, 496.7),
            (new Int32Rect(1216, 434, 204, 145), 1217, 494.3),
        };
        // 새의 자리: 그림, 게임 영역의 가운데 x, 땅선에서 밑변까지의 높이, 오른쪽을 보는가, 길 선이 오른쪽에 있는가, 선의 길이. 풍력 발전기 위를
        // 날고 서로 높이가 다르다.
        internal static readonly (int Piece, double X, double Bottom, bool Right, bool TrailRight, double Trail)[] Birds =
        {
            (0, 352, 232, true, false, 46), (1, 438, 268, false, true, 40), (2, 520, 214, true, false, 50), (0, 600, 250, false, true, 38),
        };
        private const double BirdWidth = 26;

        // 잔치(10단계): 사람(people.png — 남자아이는 떨어진 신발 한 짝까지 담도록 사각형 안을 모두 쓴다), 게임 영역의 가운데 x, 높이.
        // 뒤에는 기둥 둘 사이에 줄에 단 삼각 깃발(코드로 그림).
        private static readonly (Int32Rect Piece, double GameX, double Height)[] PartyPeople =
        {
            (new Int32Rect(157, 220, 331, 554), 762, 46), (new Int32Rect(615, 350, 173, 424), 797, 33),
            (new Int32Rect(915, 361, 211, 413), 826, 35), (new Int32Rect(1272, 218, 284, 556), 861, 47),
        };
        private const double BuntingLeft = 738, BuntingRight = 888, BuntingPoleHeight = 54;
        private static readonly Brush[] BuntingBrushes =
        {
            SceneKit.Frozen(Color.FromRgb(0xf0, 0x3e, 0x3e)), SceneKit.Frozen(Color.FromRgb(0xfd, 0x7e, 0x14)),
            SceneKit.Frozen(Color.FromRgb(0xfa, 0xb0, 0x05)), SceneKit.Frozen(Color.FromRgb(0x40, 0xc0, 0x57)),
            SceneKit.Frozen(Color.FromRgb(0x22, 0x8b, 0xe6)), SceneKit.Frozen(Color.FromRgb(0x7a, 0x4f, 0xe8)),
        };

        private const double StemScale = 0.088 * 0.62;   // 꽃밭의 줄기와 잎: 모음 그림 1px → 게임 영역 px('자라는 꽃밭' 꽃의 약 0.6배)
        private const double HeadScale = 1.2;             // 꽃은 줄기보다 이만큼 크게
        private const double HeadOverlap = 1.4;           // 꽃 아래 끝이 줄기 끝보다 이만큼(px) 아래로 내려와 줄기 끝을 덮는다

        internal static readonly Brush PanelBrush = SceneKit.Frozen(Color.FromRgb(0x2c, 0x4f, 0x7c));   // 태양광 판
        private static readonly Brush PanelLineBrush = SceneKit.Frozen(Color.FromRgb(0x9c, 0xc1, 0xe8));
        private static readonly Brush PanelFrameBrush = SceneKit.Frozen(Color.FromRgb(0xe9, 0xee, 0xf5));
        private static readonly Brush PoleBrush = SceneKit.Frozen(Color.FromRgb(0x8b, 0x5a, 0x2b));    // 깃발 기둥
        private static readonly Brush StringBrush = SceneKit.Frozen(Color.FromRgb(0x6b, 0x55, 0x40));  // 깃발 줄

        private sealed class PlantSpot
        {
            public double X;                         // 게임 영역의 가운데 x
            public int Stem, Head;                   // 줄기와 잎·꽃의 차례
            public bool StemFlip, HeadFlip;
            public double StemSize;                  // 크기 배율
        }

        private static List<PlantSpot> flowerBed;

        /// <summary>
        /// 꽃밭(5단계부터)의 자리와 모양. '자라는 꽃밭'처럼 바로 앞(꽃은 앞의 둘)과 같은 그림이 이어지지 않게 고르고 간격·크기·좌우를 조금씩
        /// 다르게 한다. 씨앗이 정해진 난수라 늘 같은 배치다.
        /// </summary>
        private static List<PlantSpot> FlowerBed
        {
            get
            {
                if (flowerBed != null) return flowerBed;
                var rnd = new Random(2610251);
                int Pick(int n, int avoid1, int avoid2)
                {
                    int v;
                    do v = rnd.Next(n); while (v == avoid1 || v == avoid2);
                    return v;
                }
                var spots = new List<PlantSpot>();
                for (double x = FlowerBedLeft; x <= FlowerBedRight; x += 15 + rnd.NextDouble() * 7)
                {
                    PlantSpot prev = spots.Count > 0 ? spots[spots.Count - 1] : null;
                    PlantSpot prev2 = spots.Count > 1 ? spots[spots.Count - 2] : null;
                    spots.Add(new PlantSpot
                    {
                        X = x,
                        Stem = Pick(PlantStems.Length, prev?.Stem ?? -1, -1),
                        Head = Pick(PlantFlowers.Length, prev?.Head ?? -1, prev2?.Head ?? -1),
                        StemFlip = rnd.Next(2) == 0,
                        HeadFlip = rnd.Next(2) == 0,
                        StemSize = 0.85 + rnd.NextDouble() * 0.3,
                    });
                }
                flowerBed = spots;
                return spots;
            }
        }

        public override void Build(ISceneCanvas canvas, int scene)
        {
            if (scene == 1)
            {
                // 연기는 굴뚝 끝에서 나오고, 공장과 같은 거리만큼 밀어 두어 함께 올라온다. 공장 그림은 가운데가 GameX 다(BuildBuilding).
                var factory = TownBuildings[FactoryIndex];
                ScenePiece fp = Piece("buildings", factory.Piece);
                if (fp != null)
                {
                    double fs = factory.Height / fp.Bitmap.PixelHeight;
                    for (int i = 0; i < SmokePlumes.Length; i++)
                    {
                        double chimneyX = factory.GameX - fp.Bitmap.PixelWidth * fs / 2 + (SmokePlumes[i].ChimneyX - fp.Left) * fs;
                        double chimneyTop = (fp.Top + fp.Bitmap.PixelHeight - SmokePlumes[i].ChimneyY) * fs;
                        (Canvas unit, double anchor) = BuildSmoke(i);
                        canvas.Add(unit, anchor, chimneyX, chimneyTop - 3, RiseKind.Ground, travel: factory.Height, fade: true);
                    }
                }
            }
            if (scene >= 3)
            {
                // 흰 발전기는 거의 흰 하늘 아래쪽에서 잘 안 보여, 메모리에서 조금 어둡게(옅은 회색) 한다.
                foreach (var t in Turbines)
                    AddSimple(canvas, SceneKit.Shaded(Piece("turbine", t.Piece), false, TurbineShade), t.Height, false, t.GameX, RiseKind.Ground);
            }
            if (scene >= 6)
            {
                foreach (var b in Birds)
                {
                    var bp = BirdPieces[b.Piece];
                    (Canvas unit, double anchor) = SceneKit.Flyer(Piece("birds", bp.Piece), bp.TipX, bp.TipY, BirdWidth, 0, b.Right, b.TrailRight, b.Trail);
                    canvas.Add(unit, anchor, b.X, b.Bottom, RiseKind.Flyer);
                }
            }
            for (int i = 0; i < Trees.Length; i++)
            {
                (Canvas unit, double anchor) = BuildTree(i, scene);
                canvas.Add(unit, anchor, Trees[i].GameX, 0, RiseKind.Ground);
            }
            for (int i = 0; i < TownBuildings.Length; i++)
            {
                (Canvas unit, double anchor) = BuildBuilding(i, scene);
                canvas.Add(unit, anchor, TownBuildings[i].GameX, 0, RiseKind.Ground);
            }
            if (scene >= 10)
            {
                (Canvas bunting, double bAnchor) = BuildBunting();
                canvas.Add(bunting, bAnchor, (BuntingLeft + BuntingRight) / 2, 0, RiseKind.Party);
                foreach (var pp in PartyPeople)
                    AddSimple(canvas, Piece("people", pp.Piece, keepAll: true), pp.Height, false, pp.GameX, RiseKind.Party);
            }
            if (scene >= 5)
            {
                foreach (var bk in Bikes)
                    AddSimple(canvas, Piece("saplings_bikes", bk.Piece), bk.Height, bk.Flip, bk.GameX, RiseKind.Ground);
            }
            if (scene >= 4)
            {
                foreach (var sp in Saplings)
                    AddSimple(canvas, Piece("saplings_bikes", sp.Piece), sp.Height, sp.Flip, sp.GameX, RiseKind.Ground);
            }
            if (scene >= 5)
            {
                foreach (PlantSpot spot in FlowerBed)
                {
                    var st = PlantStems[spot.Stem];
                    var hd = PlantFlowers[spot.Head];
                    (Canvas unit, double anchor) = SceneKit.StemWithHead(Piece("stems", st.Piece), st.StemX, st.TopY, spot.StemFlip,
                        StemScale * spot.StemSize, SceneKit.Tinted(Piece("flowers", hd.Piece, hd.StubLen), HeadTints[spot.Head]), hd.StubX,
                        spot.HeadFlip, HeadScale * FlowerSizes[spot.Head], HeadOverlap);
                    canvas.Add(unit, anchor, spot.X, 0, RiseKind.Ground);
                }
            }
        }

        /// <summary>공장 굴뚝의 연기(1단계). anchor 는 그림 속 밑동 가운데 x(뒤집으면 좌우가 바뀐다).</summary>
        private (Canvas Unit, double Anchor) BuildSmoke(int index)
        {
            var sm = SmokePlumes[index];
            ScenePiece p = Piece("smoke", sm.Piece);
            (Canvas unit, _) = SceneKit.SimpleUnit(p, sm.Height, sm.Flip);
            if (unit == null) return (null, 0);
            double ax = (sm.BottomX - p.Left) * sm.Height / p.Bitmap.PixelHeight;
            return (unit, sm.Flip ? unit.Width - ax : ax);
        }

        /// <summary>
        /// 집·공장 한 채: 1~3단계에 잿빛(Grays 인 것), 2단계부터 지붕면에 태양광 판(코드로 그린 파란 격자). anchor 는 그림 가운데 x.
        /// </summary>
        private (Canvas Unit, double Anchor) BuildBuilding(int index, int scene)
        {
            var b = TownBuildings[index];
            ScenePiece p = Piece("buildings", b.Piece);
            (Canvas unit, double anchor) = SceneKit.SimpleUnit(SceneKit.Shaded(p, b.Grays && scene <= 3, 1), b.Height, false);
            if (unit != null && scene >= 2)
            {
                double s = b.Height / p.Bitmap.PixelHeight;
                Canvas panel = SolarPanel(b.Panel.Width * s, b.Panel.Height * s, b.PanelSlant);
                Canvas.SetLeft(panel, (b.Panel.X - p.Left) * s);
                Canvas.SetTop(panel, (b.Panel.Y - p.Top) * s);
                unit.Children.Add(panel);
            }
            return (unit, anchor);
        }

        /// <summary>
        /// 태양광 판: 밝은 테두리 안의 짙은 파란 판에 가는 격자. 칸 크기는 약 9×7px. 가운데를 고정한 채 위아래 변은 수평으로 두고 옆선만
        /// slant(아래로 1px 갈 때 가로로 slant px)만큼 기울여, 지붕면과 같은 평행사변형이 되게 한다(격자의 세로선도 함께 기운다).
        /// </summary>
        private static Canvas SolarPanel(double w, double h, double slant)
        {
            var panel = new Canvas { Width = w, Height = h };
            panel.Children.Add(new Rectangle { Width = w, Height = h, Fill = PanelBrush, Stroke = PanelFrameBrush, StrokeThickness = 1 });
            int cols = Math.Max(2, (int)Math.Round(w / 9)), rows = Math.Max(1, (int)Math.Round(h / 7));
            for (int c = 1; c < cols; c++)
            {
                double x = w * c / cols;
                panel.Children.Add(new Line { X1 = x, Y1 = 1, X2 = x, Y2 = h - 1, Stroke = PanelLineBrush, StrokeThickness = 0.6 });
            }
            for (int r = 1; r < rows; r++)
            {
                double y = h * r / rows;
                panel.Children.Add(new Line { X1 = 1, Y1 = y, X2 = w - 1, Y2 = y, Stroke = PanelLineBrush, StrokeThickness = 0.6 });
            }
            panel.RenderTransform = new SkewTransform(Math.Atan(slant) * 180 / Math.PI, 0, w / 2, h / 2);
            return panel;
        }

        /// <summary>
        /// 나무 한 그루('자라는 꽃밭'의 줄기 그림): 1~3단계는 잎 없는 잿빛 줄기, 4단계부터 갈색 줄기, 5단계부터 줄기 뒤에 녹색 타원 잎.
        /// anchor 는 그림 안의 줄기 가운데 x.
        /// </summary>
        private (Canvas Unit, double Anchor) BuildTree(int index, int scene)
        {
            var t = Trees[index];
            ScenePiece p = Piece(t.File, t.Piece);
            (Canvas unit, double anchor, _) = SceneKit.Tree(p, SceneKit.Shaded(p, scene <= 3, 1), t.TrunkX, t.LowBranchY, t.Height,
                                                           t.CrownWidth, scene >= 5 ? SceneKit.CrownBrush : null);
            return (unit, anchor);
        }

        /// <summary>
        /// 잔치의 삼각 깃발(10단계): 기둥 둘 사이에 조금 처진 줄을 걸고 여러 색 삼각 깃발을 단다. 모두 코드로 그린다. anchor 는 두 기둥의
        /// 가운데.
        /// </summary>
        private static (Canvas Unit, double Anchor) BuildBunting()
        {
            double w = BuntingRight - BuntingLeft, h = BuntingPoleHeight;
            var unit = new Canvas { Width = w + 4, Height = h };
            const double Sag = 9, PoleW = 3;
            foreach (double x in new[] { 0.0, w })
                unit.Children.Add(SceneKit.PlacedRect(x, 0, PoleW, h, PoleBrush));
            // 줄: 양 기둥 끝에서 가운데로 Sag 만큼 처지는 곡선(2차 베지어). 깃발은 그 곡선 위에 같은 간격으로 단다(At).
            Point p0 = new Point(PoleW / 2, 3), p2 = new Point(w + PoleW / 2, 3), c = new Point((p0.X + p2.X) / 2, 3 + Sag * 2);
            Point At(double t) => new Point((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * c.X + t * t * p2.X,
                                            (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * c.Y + t * t * p2.Y);
            var line = new StreamGeometry();
            using (StreamGeometryContext g = line.Open())
            {
                g.BeginFigure(p0, false, false);
                g.QuadraticBezierTo(c, p2, true, true);
            }
            line.Freeze();
            unit.Children.Add(new System.Windows.Shapes.Path { Data = line, Stroke = StringBrush, StrokeThickness = 1 });
            const int Flags = 10;
            for (int i = 0; i < Flags; i++)
            {
                double ta = (i + 0.15) / Flags, tb = (i + 0.85) / Flags;
                Point a = At(ta), b = At(tb), tip = At((ta + tb) / 2);
                var tri = new StreamGeometry();
                using (StreamGeometryContext g = tri.Open())
                {
                    g.BeginFigure(a, true, true);
                    g.LineTo(b, true, false);
                    g.LineTo(new Point(tip.X, tip.Y + 10), true, false);
                }
                tri.Freeze();
                unit.Children.Add(new System.Windows.Shapes.Path { Data = tri, Fill = BuntingBrushes[i % BuntingBrushes.Length] });
            }
            return (unit, w / 2);
        }
    }
}
