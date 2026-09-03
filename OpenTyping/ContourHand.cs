using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// fingersmaple1 참고 그림(키별 실제 손 포즈 프레임)에서 추출한 실측 손 윤곽
    /// (HandContourData)을 키보드 좌표계(777x260)로 정합해 제공하는 엔진 (<260718_10>).
    /// 프레임이 있는 키는 그 프레임의 실제 포즈 윤곽을 그대로 쓰고(작동 손끝만 정확한 키
    /// 위치로 미세 스냅), 프레임이 없는 키(숫자 행, [ㅕ])만 기본자세 윤곽에서 담당 손가락
    /// 구간을 호길이 가중치로 변형해 만든다. 본체(손가락 4+손바닥)와 엄지는 별개 폐곡선.
    /// </summary>
    internal sealed class ContourHand
    {
        public struct Pt
        {
            public double X;
            public double Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        private const double BottomY = 320;      // 손목 잘림선(창 클립 아래)
        private const double CapPlateau = 0.35;  // (합성 변형) 손끝 캡 강체 비율
        private const double OuterSideFactor = 1.25; // (합성 변형) 골 없는 바깥쪽 구간 배율
        private const double SnapMaxPull = 65;   // 이보다 멀면 프레임-키 대응 이상 → 스냅 생략
        private const double SnapCapLen = 20;    // 손끝 캡(강체 이동) 축방향 길이
        private const double SnapInfluenceLen = 95; // 스냅 영향이 0이 되는 축방향 길이
        private const double DriftMax = 14;      // 프레임별 표류 보정(엄지 기준) 상한
        private const double KeyInsetX = 12;     // 최소 이동 스냅: 키 사각형 안쪽 여백

        // 홈에서 손끝(윤곽 최상점)이 놓일 위치: 홈 키 중심보다 약간 위 (새끼, 약지, 중지, 검지)
        private static readonly Pt[] HomeApexLeft =
        {
            new Pt(127, 120), new Pt(179, 110), new Pt(231, 106), new Pt(283, 110),
        };
        private static readonly Pt[] HomeApexRight =
        {
            new Pt(595, 120), new Pt(543, 110), new Pt(491, 106), new Pt(439, 110),
        };

        // ===== 키보드 좌표계 =====
        private static readonly double[] RowStartX = { 0, 82, 102, 132 };

        /// <summary>포즈 목표: 손끝(윤곽 최상점)이 키 중심보다 10 위.</summary>
        public static Pt KeyApexTarget(int row, int column) =>
            new Pt(RowStartX[row] + 52 * column + 25, 25 + 52 * row - 10);

        // [Shift]는 넓은 키라 중앙 대신 글쇠 자판 쪽 끝 부분을 누름 지점으로 삼는다
        public static readonly Pt LShiftApex = new Pt(80, 171);
        public static readonly Pt RShiftApex = new Pt(698, 171);

        // ===== 손가락 배정 (표준 지법) =====
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

        // 프레임을 쓰지 않는 키 (<260718_10> 사용자 테스트 반영):
        // 아랫행 프레임들은 손 전체가 이동한 포즈라 비작동 손가락이 홈을 벗어나고([ㅊ][ㅋ][ㅌ]
        // [ㅡ][,][.][ㅜ], [/]는 소지 부근 추출도 지저분함), [ㅠ]·[ㅑ]는 이웃 손가락 회피 동작이
        // 부자연스럽다. 이 키들은 기본자세에서 담당 손가락만 움직이는 합성 변형을 쓴다
        // (합성은 겹침을 허용 — [ㅠ]의 검지는 엄지 윤곽 위에 올라간다).
        private static readonly HashSet<(int Row, int Col)> FrameBlacklist =
            new HashSet<(int Row, int Col)>
            {
                (1, 7),                                                 // ㅑ
                (3, 0), (3, 1), (3, 2), (3, 4), (3, 5), (3, 6), (3, 7), (3, 8), (3, 9),
            };

        // 프레임 라벨 → 격자 위치 (라벨은 그 프레임이 누르는 키. Shift·home은 별도 처리)
        private static readonly Dictionary<string, (int Row, int Col)> FrameKeyMap =
            new Dictionary<string, (int Row, int Col)>
            {
                ["ㅂ"] = (1, 0), ["ㅈ"] = (1, 1), ["ㄷ"] = (1, 2), ["ㄱ"] = (1, 3), ["ㅅ"] = (1, 4),
                ["ㅛ"] = (1, 5), ["ㅕ"] = (1, 6), ["ㅑ"] = (1, 7), ["ㅐ"] = (1, 8), ["ㅔ"] = (1, 9),
                ["{"] = (1, 10), ["}"] = (1, 11),
                ["ㅁ"] = (2, 0), ["ㄴ"] = (2, 1), ["ㅇ"] = (2, 2), ["ㄹ"] = (2, 3), ["ㅎ"] = (2, 4),
                ["ㅗ"] = (2, 5), ["ㅓ"] = (2, 6), ["ㅏ"] = (2, 7), ["ㅣ"] = (2, 8), [";"] = (2, 9),
                ["'"] = (2, 10),
                ["ㅋ"] = (3, 0), ["ㅌ"] = (3, 1), ["ㅊ"] = (3, 2), ["ㅍ"] = (3, 3), ["ㅠ"] = (3, 4),
                ["ㅜ"] = (3, 5), ["ㅡ"] = (3, 6), [","] = (3, 7), ["."] = (3, 8), ["?"] = (3, 9),
            };

        // ===== 인스턴스 상태 =====
        public Pt[] HomePts;                       // 기본자세 본체 윤곽 (정합·스냅 완료)
        public Pt[] ThumbPts;                      // 기본자세 엄지 윤곽
        public int[] ApexIdx;                      // 손끝 인덱스 (새끼~검지)
        public (int Idx, double W)[][] SegWeights; // (합성 변형용) 손가락별 가중치

        private bool isRightHand;
        private double ta, tb, tmx, tmy, tdx, tdy; // 유사변환 (이미지 → 키보드)
        private readonly Dictionary<(int Row, int Col), (Pt[] Main, Pt[] Thumb)> poses =
            new Dictionary<(int Row, int Col), (Pt[] Main, Pt[] Thumb)>();

        /// <summary>기본자세 + 모든 키 프레임을 정합해 포즈 라이브러리를 만든다.</summary>
        public static ContourHand BuildLibrary(bool isRight)
        {
            string[] keys = isRight ? HandContourData.RightKeys : HandContourData.LeftKeys;
            double[][] mains = isRight ? HandContourData.RightMains : HandContourData.LeftMains;
            double[][] thumbs = isRight ? HandContourData.RightThumbs : HandContourData.LeftThumbs;

            int homeIdx = Array.IndexOf(keys, "home");
            ContourHand hand = Build(mains[homeIdx], thumbs[homeIdx], isRight);

            for (int i = 0; i < keys.Length; i++)
            {
                if (i == homeIdx) continue;
                if (keys[i] == "Shift")
                {
                    hand.RegisterPose(-2, -2, mains[i], thumbs[i]);
                }
                else if (FrameKeyMap.TryGetValue(keys[i], out (int Row, int Col) rc)
                         && !FrameBlacklist.Contains(rc))
                {
                    hand.RegisterPose(rc.Row, rc.Col, mains[i], thumbs[i]);
                }
            }
            return hand;
        }

        /// <summary>
        /// 해당 격자 위치의 포즈 윤곽(본체, 엄지)을 돌려준다.
        /// row -1 = 기본자세, row -2 = 자기 쪽 [Shift].
        /// 프레임이 없는 키는 기본자세에서 담당 손가락을 합성 변형한다.
        /// </summary>
        public (Pt[] Main, Pt[] Thumb) GetPosePts(int row, int col)
        {
            if (row == -1) return (HomePts, ThumbPts);
            if (poses.TryGetValue((row, col), out (Pt[] Main, Pt[] Thumb) stored)) return stored;

            if (row == -2)
            {
                return (Deform(0, ClampedTarget(0, row, col)), ThumbPts);
            }
            if (TryGetFinger(row, col, out _, out int finger))
            {
                return (Deform(finger, ClampedTarget(finger, row, col)), ThumbPts);
            }
            return (HomePts, ThumbPts); // 배정 없는 키(연습 제외 키)는 기본자세

            // 합성 변형도 최소 이동: 홈 손끝에서 녹색 키 사각형 안까지만 끌어온다
            Pt ClampedTarget(int f, int r, int c)
            {
                Pt anchor = HomePts[ApexIdx[f]];
                (double left, double right, double top) = KeyRect(r, c);
                return new Pt(
                    Math.Max(left + KeyInsetX, Math.Min(right - KeyInsetX, anchor.X)),
                    Math.Max(top - 12, Math.Min(top + 18, anchor.Y)));
            }
        }

        // ===== 기본자세 정합 =====

        private static ContourHand Build(double[] flatMain, double[] flatThumb, bool isRight)
        {
            int n = flatMain.Length / 3;
            var pts = new Pt[n];
            var bottom = new bool[n];
            for (int i = 0; i < n; i++)
            {
                pts[i] = new Pt(flatMain[i * 3], flatMain[i * 3 + 1]);
                bottom[i] = flatMain[i * 3 + 2] > 0.5;
            }

            // --- 1) 손끝 탐지 (이미지 좌표): 손목 중심에서의 거리 피크 ---
            var rawArc = new double[n];
            for (int i = 1; i < n; i++)
            {
                rawArc[i] = rawArc[i - 1] + Dist(pts[i], pts[i - 1]);
            }
            double rawTotal = rawArc[n - 1] + Dist(pts[0], pts[n - 1]);
            double RawArcDist(int i, int j)
            {
                double d = Math.Abs(rawArc[i] - rawArc[j]);
                return Math.Min(d, rawTotal - d);
            }

            var bottomIdx = Enumerable.Range(0, n).Where(i => bottom[i]).ToArray();
            double wx = bottomIdx.Length > 0 ? bottomIdx.Average(i => pts[i].X) : pts.Average(p => p.X);
            double wy = bottomIdx.Length > 0 ? bottomIdx.Average(i => pts[i].Y) : pts.Average(p => p.Y);
            var wrist = new Pt(wx, wy);
            var distC = pts.Select(p => Dist(p, wrist)).ToArray();

            var nearBottom = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!bottom[i]) continue;
                for (int j = 0; j < n; j++)
                {
                    if (RawArcDist(i, j) < 25) nearBottom[j] = true;
                }
            }

            var peaks = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (nearBottom[i]) continue;
                bool isPeak = true;
                for (int j = 0; j < n && isPeak; j++)
                {
                    if (j != i && RawArcDist(i, j) <= 18 && distC[j] > distC[i]) isPeak = false;
                }
                if (isPeak) peaks.Add(i);
            }
            var strongPeaks = new List<int>();
            foreach (int i in peaks.OrderByDescending(i => distC[i]))
            {
                if (strongPeaks.All(t => RawArcDist(i, t) >= 30)) strongPeaks.Add(i);
                if (strongPeaks.Count == 10) break;
            }
            var tips = strongPeaks.OrderBy(i => pts[i].Y).Take(4).ToList();
            if (tips.Count < 4)
            {
                throw new InvalidOperationException("손 윤곽 손끝 탐지 실패: tips=" + tips.Count);
            }
            var fingersByX = tips.OrderBy(i => pts[i].X).ToArray();
            int[] apexIdx = isRight ? fingersByX.Reverse().ToArray() : fingersByX;

            // --- 2) 유사변환(회전+배율+평행이동): 손끝 4점 → 홈 목표 (최소제곱) ---
            Pt[] homeApex = isRight ? HomeApexRight : HomeApexLeft;
            double msx = 0, msy = 0, mdx = 0, mdy = 0;
            for (int f = 0; f < 4; f++)
            {
                msx += pts[apexIdx[f]].X / 4; msy += pts[apexIdx[f]].Y / 4;
                mdx += homeApex[f].X / 4; mdy += homeApex[f].Y / 4;
            }
            double num = 0, cross = 0, den = 0;
            for (int f = 0; f < 4; f++)
            {
                double sx = pts[apexIdx[f]].X - msx, sy = pts[apexIdx[f]].Y - msy;
                double dx = homeApex[f].X - mdx, dy = homeApex[f].Y - mdy;
                num += sx * dx + sy * dy;
                cross += sx * dy - sy * dx;
                den += sx * sx + sy * sy;
            }
            var hand = new ContourHand
            {
                isRightHand = isRight,
                ta = num / den,
                tb = cross / den,
                tmx = msx,
                tmy = msy,
                tdx = mdx,
                tdy = mdy,
            };
            for (int i = 0; i < n; i++)
            {
                pts[i] = hand.TransformPoint(pts[i], bottom[i]);
            }

            // --- 3) 호길이 누적 (키보드 좌표) ---
            var arc = new double[n];
            for (int i = 1; i < n; i++)
            {
                arc[i] = arc[i - 1] + Dist(pts[i], pts[i - 1]);
            }
            double total = arc[n - 1] + Dist(pts[0], pts[n - 1]);
            double ArcDist(int i, int j)
            {
                double d = Math.Abs(arc[i] - arc[j]);
                return Math.Min(d, total - d);
            }

            // --- 4) 골(손가락 사이 홈): 이웃 손끝 사이 "짧은 호" 위의 y 최댓값 ---
            var valleyOf = new int[3];
            for (int k = 0; k < 3; k++)
            {
                int i0 = apexIdx[k], i1 = apexIdx[k + 1];
                int fwd = (i1 - i0 + n) % n;
                int step = fwd <= n - fwd ? 1 : -1;
                int steps = Math.Min(fwd, n - fwd);
                int vi = i0;
                for (int s = 0, i = i0; s <= steps; s++, i = (i + step + n) % n)
                {
                    if (pts[i].Y > pts[vi].Y) vi = i;
                }
                valleyOf[k] = vi;
            }

            // --- 5) (합성 변형용) 손가락별 변형 구간·가중치 ---
            var segWeights = new (int Idx, double W)[4][];
            for (int f = 0; f < 4; f++)
            {
                int ai = apexIdx[f];
                double dToPrev = f > 0 ? ArcDist(ai, valleyOf[f - 1]) : -1;
                double dToNext = f < 3 ? ArcDist(ai, valleyOf[f]) : -1;
                if (dToPrev < 0) dToPrev = dToNext * OuterSideFactor;
                if (dToNext < 0) dToNext = dToPrev * OuterSideFactor;
                double dPlus, dMinus;
                {
                    int vi = f > 0 ? valleyOf[f - 1] : valleyOf[0];
                    double dRef = f > 0 ? dToPrev : dToNext;
                    double dOther = f > 0 ? dToNext : dToPrev;
                    bool refIsPlus = (vi - ai + n) % n <= (ai - vi + n) % n;
                    dPlus = refIsPlus ? dRef : dOther;
                    dMinus = refIsPlus ? dOther : dRef;
                }

                var ws = new List<(int, double)>();
                for (int i = 0; i < n; i++)
                {
                    if (bottom[i]) continue;
                    double d = ArcDist(i, ai);
                    bool plusSide = (i - ai + n) % n <= (ai - i + n) % n;
                    double dSide = plusSide ? dPlus : dMinus;
                    if (d >= dSide) continue;
                    double t = Math.Max(0, (d / dSide - CapPlateau) / (1 - CapPlateau));
                    double w = 0.5 * (1 + Math.Cos(Math.PI * Math.Min(1, t)));
                    if (w > 0.003) ws.Add((i, w));
                }
                segWeights[f] = ws.ToArray();
            }

            hand.HomePts = pts;
            hand.ThumbPts = hand.TransformFlat(flatThumb);
            hand.ApexIdx = apexIdx;
            hand.SegWeights = segWeights;

            // --- 6) 스냅: 각 손끝을 정확히 홈 목표 위로 (유사변환 잔차 보정) ---
            for (int f = 0; f < 4; f++)
            {
                Pt apex = pts[apexIdx[f]];
                double dx = homeApex[f].X - apex.X, dy = homeApex[f].Y - apex.Y;
                foreach (var (idx, w) in segWeights[f])
                {
                    pts[idx] = new Pt(pts[idx].X + dx * w, pts[idx].Y + dy * w);
                }
            }
            return hand;
        }

        // ===== 키 프레임 포즈 등록 =====

        /// <summary>
        /// 키 프레임의 실측 포즈 윤곽을 등록한다. 기본자세와 같은 유사변환으로 정합하고,
        /// (일반 키 프레임은) 정지해 있는 엄지의 중심 이동으로 프레임별 표류를 먼저 지운 뒤,
        /// 작동 손끝을 녹색 키 위로 미세 스냅한다. 스냅 목표는 키 중심이 아니라 "키 사각형
        /// 안까지의 최소 이동"이라 프레임 원형이 최대한 보존되고, 가중치는 손가락 축 방향
        /// (손끝 캡 강체 + 축 아래로 감쇠 + 측면은 부드러운 감쇠)이라 손가락이 꺾이지 않는다.
        /// (반경형 감쇠와 급격한 측면 차단은 손가락을 꺾거나 윤곽에 단차를 만들므로 금지.)
        /// </summary>
        private void RegisterPose(int row, int col, double[] flatMain, double[] flatThumb)
        {
            Pt[] main = TransformFlat(flatMain);
            Pt[] thumb = TransformFlat(flatThumb);

            // 표류 보정: 애니메이션에서 엄지는 [Space] 위에 정지해 있으므로, 엄지 중심의
            // 기본자세 대비 이동량 = 프레임 전체의 표류다. (Shift 프레임은 손 전체가
            // 실제로 움직인 포즈라 보정하지 않는다.)
            if (row >= 0 && thumb.Length > 2 && ThumbPts != null && ThumbPts.Length > 2)
            {
                Pt c0 = Centroid(ThumbPts);
                Pt c1 = Centroid(thumb);
                double ddx = c0.X - c1.X, ddy = c0.Y - c1.Y;
                double dl = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (dl > DriftMax) { ddx *= DriftMax / dl; ddy *= DriftMax / dl; }
                for (int i = 0; i < main.Length; i++)
                {
                    if (main[i].Y >= BottomY - 0.5) continue; // 손목 잘림선은 그대로
                    main[i] = new Pt(main[i].X + ddx, main[i].Y + ddy);
                }
                for (int i = 0; i < thumb.Length; i++)
                {
                    if (thumb[i].Y >= BottomY - 0.5) continue;
                    thumb[i] = new Pt(thumb[i].X + ddx, thumb[i].Y + ddy);
                }
            }

            // 작동 손끝 = 명목 목표(키 위 손끝 자리)에 가장 가까운 본체 점
            Pt nominal = row == -2
                ? (isRightHand ? RShiftApex : LShiftApex)
                : KeyApexTarget(row, col);
            int n = main.Length;
            int nearest = 0;
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double d2 = (main[i].X - nominal.X) * (main[i].X - nominal.X)
                          + (main[i].Y - nominal.Y) * (main[i].Y - nominal.Y);
                if (d2 < best) { best = d2; nearest = i; }
            }
            Pt anchor = main[nearest];

            // 최소 이동 목표: 손끝이 이미 녹색 키 사각형 안이면 그대로, 밖이면 사각형 안까지만
            (double left, double right, double top) = KeyRect(row, col);
            var target = new Pt(
                Math.Max(left + KeyInsetX, Math.Min(right - KeyInsetX, anchor.X)),
                Math.Max(top - 12, Math.Min(top + 18, anchor.Y)));

            double dx = target.X - anchor.X, dy = target.Y - anchor.Y;
            double pull = Math.Sqrt(dx * dx + dy * dy);
            if (pull > 0.5 && pull <= SnapMaxPull)
            {
                // 손가락 축: 손끝 양옆으로 18점 내려간 두 윤곽점의 중점 → 손끝 방향
                Pt sideA = main[(nearest - 18 + n) % n];
                Pt sideB = main[(nearest + 18) % n];
                var mid = new Pt((sideA.X + sideB.X) / 2, (sideA.Y + sideB.Y) / 2);
                double ax = anchor.X - mid.X, ay = anchor.Y - mid.Y;
                double al = Math.Sqrt(ax * ax + ay * ay);
                if (al > 1e-6)
                {
                    ax /= al; ay /= al;
                    for (int i = 0; i < n; i++)
                    {
                        double vx = main[i].X - anchor.X, vy = main[i].Y - anchor.Y;
                        double s = -(vx * ax + vy * ay);            // 손끝에서 아래로 갈수록 양수
                        double lat = Math.Abs(-vx * ay + vy * ax);  // 축 수직 거리
                        double latW = LateralWeight(lat);
                        if (s >= SnapInfluenceLen || latW <= 0) continue;
                        double t = Math.Max(0, (s - SnapCapLen) / (SnapInfluenceLen - SnapCapLen));
                        double w = 0.5 * (1 + Math.Cos(Math.PI * Math.Min(1, t))) * latW;
                        main[i] = new Pt(main[i].X + dx * w, main[i].Y + dy * w);
                    }
                }
            }

            poses[(row, col)] = (main, thumb);
        }

        /// <summary>녹색 키 사각형 (row -2 = 자기 쪽 [Shift]).</summary>
        private (double Left, double Right, double Top) KeyRect(int row, int col)
        {
            if (row == -2)
            {
                return isRightHand ? (652.0, 777.0, 156.0) : (0.0, 130.0, 156.0);
            }
            double left = RowStartX[row] + 52 * col;
            return (left, left + 50, 52.0 * row);
        }

        /// <summary>측면(손가락 축 수직) 감쇠: 16px까지 1, 34px에서 0으로 부드럽게.</summary>
        private static double LateralWeight(double lat) =>
            lat <= 16 ? 1 : lat >= 34 ? 0 : 0.5 * (1 + Math.Cos(Math.PI * (lat - 16) / 18));

        /// <summary>굽힘(아래 이동)용 좁은 측면 감쇠: 10px까지 1, 22px에서 0.</summary>
        private static double CurlLateralWeight(double lat) =>
            lat <= 10 ? 1 : lat >= 22 ? 0 : 0.5 * (1 + Math.Cos(Math.PI * (lat - 10) / 12));

        private static Pt Centroid(Pt[] pts)
        {
            double sx = 0, sy = 0;
            foreach (Pt p in pts) { sx += p.X; sy += p.Y; }
            return new Pt(sx / pts.Length, sy / pts.Length);
        }

        // ===== 합성 변형 (프레임 없는 키용) =====

        /// <summary>
        /// 기본자세에서 담당 손가락만 목표 지점으로 움직인 윤곽을 돌려준다.
        /// 프레임 스냅과 같은 손가락 축 방향 가중(손끝 캡 강체 + 축 아래로 감쇠 + 부드러운
        /// 측면 감쇠)이라 멀리 뻗어도 손가락이 가늘어지거나 꺾이지 않고, 영향 길이가 이동
        /// 거리에 맞춰 늘어나 손가락 전체가 매끄럽게 늘어난다.
        /// </summary>
        public Pt[] Deform(int finger, Pt target)
        {
            var outPts = (Pt[])HomePts.Clone();
            if (finger < 0) return outPts;

            int n = outPts.Length;
            int ai = ApexIdx[finger];
            Pt anchor = HomePts[ai];
            double dx = target.X - anchor.X, dy = target.Y - anchor.Y;
            double pull = Math.Sqrt(dx * dx + dy * dy);
            if (pull < 0.5) return outPts;

            Pt sideA = HomePts[(ai - 18 + n) % n];
            Pt sideB = HomePts[(ai + 18) % n];
            var mid = new Pt((sideA.X + sideB.X) / 2, (sideA.Y + sideB.Y) / 2);
            double ax = anchor.X - mid.X, ay = anchor.Y - mid.Y;
            double al = Math.Sqrt(ax * ax + ay * ay);
            if (al < 1e-6) return outPts;
            ax /= al; ay /= al;

            // 아래로 굽히는 경우(dy>0): 측면 대역을 좁혀 손가락 중심 기둥만 내려가게 한다.
            // 슬릿(손가락 사이 틈) 벽까지 끌면 틈이 벌어져 고리 모양 구멍이 생긴다.
            // 손끝 캡이 손가락 안쪽으로 겹쳐 들어가는 것은 타자몬 참고 그림의 굽힘 표현과 같다.
            bool isCurl = dy > 0;
            double influence = Math.Max(SnapInfluenceLen, pull * 0.9 + 30);
            for (int i = 0; i < n; i++)
            {
                if (HomePts[i].Y >= BottomY - 0.5) continue; // 손목 잘림선은 그대로
                double vx = HomePts[i].X - anchor.X, vy = HomePts[i].Y - anchor.Y;
                double s = -(vx * ax + vy * ay);
                double lat = Math.Abs(-vx * ay + vy * ax);
                double latW = isCurl ? CurlLateralWeight(lat) : LateralWeight(lat);
                if (s >= influence || latW <= 0) continue;
                double t = Math.Max(0, (s - SnapCapLen) / (influence - SnapCapLen));
                double w = 0.5 * (1 + Math.Cos(Math.PI * Math.Min(1, t))) * latW;
                outPts[i] = new Pt(outPts[i].X + dx * w, outPts[i].Y + dy * w);
            }
            return outPts;
        }

        // ===== 도우미 =====

        private Pt TransformPoint(Pt p, bool isBottom)
        {
            double sx = p.X - tmx, sy = p.Y - tmy;
            double x = ta * sx - tb * sy + tdx;
            double y = tb * sx + ta * sy + tdy;
            return new Pt(x, isBottom ? BottomY : y);
        }

        private Pt[] TransformFlat(double[] flat)
        {
            int n = flat.Length / 3;
            var pts = new Pt[n];
            for (int i = 0; i < n; i++)
            {
                pts[i] = TransformPoint(new Pt(flat[i * 3], flat[i * 3 + 1]), flat[i * 3 + 2] > 0.5);
            }
            return pts;
        }

        private static double Dist(Pt a, Pt b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
