using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OpenTyping
{
    /// <summary>
    /// 지면 클립 아트가 레벨에 따라 땅에서 올라오는 방식의 종류(acidrain-bg3 의 <261024_1>(3.2.3) 규칙). Ground = 땅의 것(나무·식물·건물
    /// 등), Flyer = 날벌레·새, Party = 마지막 장면의 주인공(나비, 잔치의 사람·깃발).
    /// </summary>
    internal enum RiseKind { Ground, Flyer, Party }

    /// <summary>배경 세트가 지면 클립 아트를 놓는 곳(오락 창의 SceneryLayer).</summary>
    internal interface ISceneCanvas
    {
        /// <summary>
        /// unit 안의 x 위치 anchor 가 게임 영역의 gameX 에 오도록, 밑변을 땅선에서 bottom 위에 놓는다. travel 을 주지 않으면 다 숨었을 때
        /// 그림 위 끝까지 땅속으로 밀어 둔다. fade 면 올라온 만큼 진해진다. unit 이 null 이면(그림을 못 읽음) 아무것도 하지 않는다.
        /// </summary>
        void Add(Canvas unit, double anchor, double gameX, double bottom, RiseKind kind, double? travel = null, bool fade = false);
    }

    /// <summary>
    /// 산성비 오락의 `배경 세트`(<261025_3>): 배경 하나의 그림(단계별 모든 그림 — Resources\AcidRain\&lt;Id&gt;\*.png)과 그 그림을 단계에
    /// 맞춰 놓는 코드의 묶음. 새 배경은 이 클래스를 이어받은 클래스 하나와 그림 폴더 하나로 만들고 BackgroundSets.All 에 한 줄 더한다.
    /// 자리는 게임 영역(너비 900) 좌표이고, 지면 클립 아트의 높이는 땅선(SceneryLayer 바닥 = 땅 띠 윗선)에서 위로 잰다. 장면의 하늘판은
    /// 쓰지 않는다 — 이 창의 하늘은 생명이 줄수록 주황으로 물드는 그라데이션(UpdateSky)이라, 그림으로 덮으면 그 표시가 사라진다.
    /// </summary>
    internal abstract class BackgroundSet
    {
        /// <summary>코드·설정에 쓰는 영문 이름이자 그림 폴더 이름. 한 번 정하면 바꾸지 않는다(나중에 사용자의 선택을 저장할 때 쓰인다).</summary>
        public abstract string Id { get; }

        /// <summary>화면에 보일 이름(예: '자라는 꽃밭').</summary>
        public abstract string Name { get; }

        /// <summary>
        /// (계획) 오락 전에 배경을 고르는 드롭다운에 보일지. false 면 프로그램에는 들어 있되 메뉴에만 보이지 않는다 — 자판별 배정
        /// (BackgroundSets.ByKeyboard)으로는 여전히 쓸 수 있다.
        /// </summary>
        public virtual bool ShowInMenu => true;

        /// <summary>그림 폴더 안의 그림 파일 이름(확장자 뺀 것). 진단(-acidscenetest)이 출처 정보(C2PA)가 남았는지 본다.</summary>
        public abstract IReadOnlyList<string> SheetFiles { get; }

        /// <summary>
        /// 배경 단계(이 배경의 이야기 단계 번호)를 오락 단계 차례대로 적은 것. 따로 맞춘 자판이 없을 때 쓰고, 오락 단계 수와 다르면
        /// BackgroundSets.SceneFor 의 불일치 규칙을 따른다.
        /// </summary>
        public abstract IReadOnlyList<int> Stages { get; }

        /// <summary>자판(단계 묶음의 key, 예: "ko"·"en")에 따로 맞춘 배경 단계 차례. 없으면 null(Stages 를 쓴다).</summary>
        public virtual IReadOnlyList<int> StagesFor(string keyboardKey) => null;

        /// <summary>구름 그림 파일과 구름(그 그림 속 조각 → 게임 영역의 왼쪽·위·너비 — 흰 구름(배율 1) 기준).</summary>
        public virtual string CloudFile => "clouds";
        public abstract IReadOnlyList<(Int32Rect Piece, double Left, double Top, double Width)> Clouds { get; }

        /// <summary>배경 단계 scene 의 구름(Scale = 크기 배율 — 가운데를 둔 채, Shade = 흰 구름에 곱하는 밝기). 기본은 늘 흰 구름.</summary>
        public virtual (double Scale, double Shade) CloudLook(int scene) => (1, 1);

        /// <summary>레벨 level 에서 이 종류의 지면 클립 아트가 얼마나 올라왔나(0~1). 기본은 BackgroundSets.RiseFraction.</summary>
        public virtual double RiseFraction(RiseKind kind, int scene, int level) => BackgroundSets.RiseFraction(kind, scene, level);

        /// <summary>배경 단계 scene 의 지면 클립 아트를 canvas 에 놓는다(구름은 오락 창이 Clouds·CloudLook 로 놓는다).</summary>
        public abstract void Build(ISceneCanvas canvas, int scene);

        /// <summary>이 배경 세트의 그림 폴더에서 조각을 떼어 낸다(SceneKit.SheetPiece).</summary>
        protected ScenePiece Piece(string file, Int32Rect rect, int cutBottom = 0, bool keepAll = false) =>
            SceneKit.SheetPiece(Id, file, rect, cutBottom, keepAll);

        /// <summary>그림 한 장짜리 지면 클립 아트(SceneKit.SimpleUnit)를 가운데가 게임 영역의 gameX 에 오게 땅선 위에 놓는다.</summary>
        protected static void AddSimple(ISceneCanvas canvas, ScenePiece p, double height, bool flip, double gameX, RiseKind kind)
        {
            (Canvas unit, double anchor) = SceneKit.SimpleUnit(p, height, flip);
            canvas.Add(unit, anchor, gameX, 0, kind);
        }
    }
}
