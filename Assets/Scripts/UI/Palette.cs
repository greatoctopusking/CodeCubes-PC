using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>全部界面配色。占位图形方案下，颜色是唯一的视觉区分手段。</summary>
    public static class Palette
    {
        // 背景与面板
        public static readonly Color ScreenBg = new Color(0.09f, 0.10f, 0.13f, 1f);
        public static readonly Color TopBarBg = new Color(0.14f, 0.16f, 0.20f, 1f);
        public static readonly Color BoardBg = new Color(0.13f, 0.15f, 0.19f, 1f);
        public static readonly Color PanelBg = new Color(0.15f, 0.17f, 0.22f, 1f);
        public static readonly Color PanelBgAlt = new Color(0.11f, 0.13f, 0.17f, 1f);
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.10f);

        // 格子
        public static readonly Color TileLight = new Color(0.90f, 0.92f, 0.96f, 1f);
        public static readonly Color TileDark = new Color(0.79f, 0.83f, 0.91f, 1f);

        // 机器人与星星
        public static readonly Color Robot = new Color(0.20f, 0.55f, 1f, 1f);
        public static readonly Color RobotOutline = new Color(0.08f, 0.25f, 0.55f, 1f);
        public static readonly Color FacingArrow = new Color(0.30f, 0.70f, 1f, 0.95f);
        public static readonly Color Star = new Color(1f, 0.82f, 0.15f, 1f);

        // 连线
        public static readonly Color LineNext = new Color(1f, 0.85f, 0f, 0.90f);
        public static readonly Color LineJudger = new Color(0.20f, 0.90f, 0.30f, 0.90f);
        public static readonly Color LinePreview = new Color(0.30f, 0.70f, 1f, 0.85f);

        // 交互
        public static readonly Color Highlight = new Color(1f, 0.92f, 0.30f, 1f);
        public static readonly Color HighlightText = new Color(0.12f, 0.10f, 0.02f, 1f);
        public static readonly Color BlockDim = new Color(1f, 1f, 1f, 0.35f);
        public static readonly Color TrashReady = new Color(0.72f, 0.30f, 0.30f, 0.85f);
        public static readonly Color TrashHot = new Color(1f, 0.42f, 0.38f, 0.95f);
        public static readonly Color TrashDisabled = new Color(0.35f, 0.35f, 0.38f, 0.55f);

        // 文本
        public static readonly Color TextMain = new Color(0.97f, 0.97f, 0.99f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.66f, 0.74f, 1f);
        public static readonly Color TextError = new Color(1f, 0.82f, 0.28f, 1f);

        // 按钮
        public static readonly Color ButtonBg = new Color(0.24f, 0.42f, 0.78f, 1f);
        public static readonly Color ButtonBgAlt = new Color(0.26f, 0.29f, 0.36f, 1f);
        public static readonly Color PanelOverlay = new Color(0.05f, 0.06f, 0.08f, 0.82f);
    }
}
