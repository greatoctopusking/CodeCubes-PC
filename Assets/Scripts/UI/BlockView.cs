using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 单个代码块的视觉表现与位置。
    ///
    /// 分工：BlockView 只负责「长什么样」和「在程序区的坐标」，
    /// 「被拖动 / 被右键点击」由 WorkspaceView 统一做命中测试后调用，
    /// 避免 uGUI 射线与自绘线条互相抢事件。
    ///
    /// 端口约定（与原版一致：连线连的是块本身，不是特定端口）：
    ///   OutputPort 右边缘 —— next 黄线的起点
    ///   InputPort  左边缘 —— next 黄线的终点
    ///   CondSlot   下边缘 —— While/IF 的条件槽（绿线起点）
    ///   TopPort    上边缘 —— 条件块被连到 While/IF 时的接点（绿线终点）
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        public const float Width = 176f;
        public const float Height = 42f;
        private const float PortSize = 9f;

        private RectTransform rect;
        private Image background;
        private Text label;
        private Image outDot;
        private Image inDot;
        private Image condDot;
        private Color normalColor;
        private Color normalTextColor;
        private bool highlighted;

        public CodeBlock Block { get; private set; }

        public RectTransform Rect { get { return rect; } }

        public BlockType Type
        {
            get { return Block != null ? Block.type : BlockType.MoveForward; }
        }

        public static BlockView Create(Transform parent, BlockType type)
        {
            // GameObject 名直接用显示名，这样校验器的报错（如 Cycle detected: 'X'）
            // 与原项目的可读性一致。
            RectTransform rt = UIFactory.CreateRect(BlockTypes.DisplayName(type), parent);
            var view = rt.gameObject.AddComponent<BlockView>();
            view.Build(type);
            return view;
        }

        private void Build(BlockType type)
        {
            rect = GetComponent<RectTransform>();
            UIFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(Width, Height));

            background = gameObject.AddComponent<Image>();
            background.sprite = SpriteFactory.Rounded();
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;
            normalColor = BlockTypes.BaseColor(type);
            normalTextColor = BlockTypes.TextColor(type);
            background.color = normalColor;

            label = UIFactory.CreateText("Label", transform, BlockTypes.DisplayName(type), 14,
                normalTextColor, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform, new Vector2(10f, 1f), new Vector2(-10f, -1f));

            Sprite dot = SpriteFactory.Circle();

            outDot = MakeDot("OutDot", new Vector2(Width * 0.5f - 4f, 0f), dot,
                new Color(1f, 1f, 1f, 0.85f));
            inDot = MakeDot("InDot", new Vector2(-Width * 0.5f + 4f, 0f), dot,
                new Color(1f, 1f, 1f, 0.45f));

            if (BlockTypes.HasJudgerSlot(type))
            {
                condDot = MakeDot("CondDot", new Vector2(0f, -Height * 0.5f + 4f), dot, Palette.LineJudger);
            }
            else if (BlockTypes.IsBool(type))
            {
                condDot = MakeDot("CondTopDot", new Vector2(0f, Height * 0.5f - 4f), dot, Palette.LineJudger);
            }
        }

        private Image MakeDot(string name, Vector2 position, Sprite sprite, Color color)
        {
            Image image = UIFactory.CreatePanel(name, transform, color, false, false);
            image.sprite = sprite;
            UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(PortSize, PortSize));
            return image;
        }

        public void BindBlock(CodeBlock block)
        {
            Block = block;
            block.AttachView(this);
        }

        // ------------------------------------------------------------------
        // 位置与端口（父级局部坐标，与 UILineGraphic 使用同一坐标系）
        // ------------------------------------------------------------------

        public Vector2 Position
        {
            get { return rect.anchoredPosition; }
            set { rect.anchoredPosition = value; }
        }

        public Vector2 OutputPort
        {
            get { return rect.anchoredPosition + new Vector2(Width * 0.5f, 0f); }
        }

        public Vector2 InputPort
        {
            get { return rect.anchoredPosition + new Vector2(-Width * 0.5f, 0f); }
        }

        public Vector2 CondSlot
        {
            get { return rect.anchoredPosition + new Vector2(0f, -Height * 0.5f); }
        }

        public Vector2 TopPort
        {
            get { return rect.anchoredPosition + new Vector2(0f, Height * 0.5f); }
        }

        public void SetPosition(Vector2 value)
        {
            rect.anchoredPosition = value;
        }

        /// <summary>把块限制在程序区内容范围内，避免拖到看不见的地方。</summary>
        public void ClampToBounds(Vector2 min, Vector2 max)
        {
            Vector2 pos = rect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, min.x + Width * 0.5f, max.x - Width * 0.5f);
            pos.y = Mathf.Clamp(pos.y, min.y + Height * 0.5f, max.y - Height * 0.5f);
            rect.anchoredPosition = pos;
        }

        // ------------------------------------------------------------------
        // 外观状态
        // ------------------------------------------------------------------

        /// <summary>原 Code.SetHighlight：高亮为黄色。</summary>
        public void SetHighlight(bool active)
        {
            highlighted = active;
            ApplyColors();
        }

        public bool IsHighlighted { get { return highlighted; } }

        private void ApplyColors()
        {
            if (background != null)
                background.color = highlighted ? Palette.Highlight : normalColor;

            if (label != null)
                label.color = highlighted ? Palette.HighlightText : normalTextColor;
        }
    }
}
