using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 屏幕下半部分左侧：本关所有可用代码块的列表，并显示每种块的**剩余数量**。
    ///
    /// 对应原项目的软木板（CodeBlockBoard + CodeBlockSlot）：
    ///   * 只列出本关 suggestedBlockNames 允许的类型（原 ApplyAvailableBlocks 的过滤）
    ///   * 每种的数量 = min(Catalog 的 3, 本关 GetCopyCap) —— 见 LevelPolicy
    ///   * 拖出一个就少一个，**不补货**（原实现抓取后槽位变空）
    ///   * 归还后回到同类型的空位；剩余 = 上限 时拒绝归还
    ///
    /// 交互：鼠标左键从行上拖出 → 在工作区生成一个块（由 GameController 统一处理拖动）。
    /// 滚轮在此面板上滚动列表。
    /// </summary>
    public class InventoryView : MonoBehaviour
    {
        public const float PanelWidth = 520f;

        private const float HeaderHeight = 50f;

        /// <summary>
        /// 行高的上下限。实际行高按视口高度自动反算，目标是一屏放下本关所有块类型
        /// （第 10–12 关有 12 种，16:9 下行高约 33px，正好放满）。
        /// </summary>
        private const float MaxRowHeight = 52f;
        private const float MinRowHeight = 28f;

        private const float RowSpacing = 6f;
        private const float SidePadding = 12f;

        /// <summary>16:9 下的视口高度（540 - 56）。布局尚未算出来时用它兜底。</summary>
        private const float FallbackViewportHeight = 484f;

        private const float ScrollBarWidth = 6f;

        private class Row
        {
            public BlockType type;
            public RectTransform rect;
            public Image background;
            public Text nameText;
            public Text countText;
            public Color baseColor;
            public Color baseTextColor;
            public bool available;
            public bool hovered;
        }

        private RectTransform root;
        private RectTransform viewport;
        private RectTransform content;
        private Text headerText;

        private Image scrollTrack;
        private Image scrollThumb;
        private float lastViewportHeight = -1f;

        private readonly List<Row> rows = new List<Row>();
        private BlockPool pool;
        private float scroll;
        private BlockType hoveredType;
        private bool hasHoveredType;

        public RectTransform Viewport { get { return viewport; } }

        // ------------------------------------------------------------------

        public void Build(Transform parent)
        {
            Image panel = UIFactory.CreatePanel("InventoryPanel", parent, Palette.PanelBg, false, false);
            root = panel.rectTransform;
            UIFactory.Anchor(root, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(PanelWidth, 0f));

            headerText = UIFactory.CreateText("Header", root, "Blocks", 22,
                Palette.TextMain, TextAnchor.MiddleLeft);
            UIFactory.Place(headerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(SidePadding, -8f), new Vector2(PanelWidth - SidePadding * 2f, 34f));

            // 滚动视口（手写滚动，避免 ScrollRect 与「拖出生成块」抢同一个拖拽事件）
            viewport = UIFactory.CreateRect("Viewport", root);
            UIFactory.Anchor(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, -(HeaderHeight + 6f)));
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            BuildScrollBar();
        }

        /// <summary>
        /// 右侧细滚动条。挂在 root 下而不是 viewport 下 —— viewport 上有 RectMask2D，
        /// 放进去会被一起裁掉。只在内容确实装不下时才显示（16:9 下第 10-12 关也不需要）。
        /// </summary>
        private void BuildScrollBar()
        {
            Image track = UIFactory.CreatePanel("ScrollTrack", root, Palette.PanelBgAlt, true, false);
            scrollTrack = track;
            UIFactory.Anchor(track.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-ScrollBarWidth - 4f, 6f),
                new Vector2(-4f, -(HeaderHeight + 6f) - 6f));

            Image thumb = UIFactory.CreatePanel("ScrollThumb", track.transform, Palette.TextDim, true, false);
            scrollThumb = thumb;
            UIFactory.Place(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(ScrollBarWidth, 40f));

            track.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        public void Init(BlockPool blockPool)
        {
            pool = blockPool;
            if (pool != null)
                pool.Changed += OnPoolChanged;
        }

        private void OnDestroy()
        {
            if (pool != null)
                pool.Changed -= OnPoolChanged;
        }

        private void OnPoolChanged()
        {
            RefreshCounts();
        }

        // ------------------------------------------------------------------
        // 内容构建
        // ------------------------------------------------------------------

        /// <summary>进入关卡时重建列表。</summary>
        public void Rebuild()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].rect != null)
                    Destroy(rows[i].rect.gameObject);
            }
            rows.Clear();

            scroll = 0f;

            if (pool == null)
                return;

            List<BlockType> types = pool.OrderedTypes();
            float rowWidth = PanelWidth - SidePadding * 2f;

            for (int i = 0; i < types.Count; i++)
            {
                BlockType type = types[i];

                Image rowImage = UIFactory.CreatePanel(BlockTypes.DisplayName(type), content,
                    BlockTypes.BaseColor(type), true, false);

                RectTransform rect = rowImage.rectTransform;
                UIFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -i * (MaxRowHeight + RowSpacing)),
                    new Vector2(rowWidth, MaxRowHeight));

                Text nameText = UIFactory.CreateText("Name", rect, BlockTypes.DisplayName(type), 16,
                    BlockTypes.TextColor(type), TextAnchor.MiddleLeft);
                UIFactory.Stretch(nameText.rectTransform, new Vector2(14f, 0f), new Vector2(-70f, 0f));

                Text countText = UIFactory.CreateText("Count", rect, "0 / 0", 17,
                    BlockTypes.TextColor(type), TextAnchor.MiddleRight);
                UIFactory.Stretch(countText.rectTransform, new Vector2(14f, 0f), new Vector2(-14f, 0f));

                var row = new Row();
                row.type = type;
                row.rect = rect;
                row.background = rowImage;
                row.nameText = nameText;
                row.countText = countText;
                row.baseColor = BlockTypes.BaseColor(type);
                row.baseTextColor = BlockTypes.TextColor(type);
                rows.Add(row);
            }

            LayoutRows();
            RefreshCounts();
        }

        /// <summary>
        /// 按视口高度反算行高，目标是一屏放下本关所有块类型
        /// （第 10-12 关有 12 种；16:9 下算得行高约 33px，正好铺满视口）。
        /// 只有窗口比例特殊或面板很矮、实在放不下时才退回滚动，并显示滚动条。
        /// </summary>
        private void LayoutRows()
        {
            if (viewport == null || content == null)
                return;

            float rowWidth = PanelWidth - SidePadding * 2f;
            float viewportHeight = viewport.rect.height;
            lastViewportHeight = viewportHeight;

            if (viewportHeight < 1f)
                viewportHeight = FallbackViewportHeight;

            if (rows.Count == 0)
            {
                content.sizeDelta = new Vector2(rowWidth, 0f);
                UpdateScrollBar(0f, viewportHeight);
                return;
            }

            // 目标：rows.Count * (rowHeight + RowSpacing) + SidePadding <= viewportHeight
            float available = Mathf.Max(0f, viewportHeight - SidePadding);
            float fit = available / rows.Count - RowSpacing;
            float rowHeight = Mathf.Clamp(fit, MinRowHeight, MaxRowHeight);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.rect == null)
                    continue;

                UIFactory.Place(row.rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -i * (rowHeight + RowSpacing)),
                    new Vector2(rowWidth, rowHeight));
            }

            float totalHeight = rows.Count * (rowHeight + RowSpacing) + SidePadding;
            content.sizeDelta = new Vector2(rowWidth, totalHeight);

            float maxScroll = Mathf.Max(0f, totalHeight - viewportHeight);
            scroll = Mathf.Clamp(scroll, -maxScroll, 0f);
            content.anchoredPosition = new Vector2(0f, scroll);

            UpdateScrollBar(maxScroll, viewportHeight);
        }

        private void UpdateScrollBar(float maxScroll, float viewportHeight)
        {
            if (scrollTrack == null || scrollThumb == null)
                return;

            bool needed = maxScroll > 0.5f;
            if (scrollTrack.gameObject.activeSelf != needed)
                scrollTrack.gameObject.SetActive(needed);

            if (!needed)
                return;

            float trackHeight = scrollTrack.rectTransform.rect.height;
            float contentHeight = Mathf.Max(0.01f, content.rect.height);
            float ratio = Mathf.Clamp01(viewportHeight / contentHeight);
            float thumbHeight = Mathf.Clamp(trackHeight * ratio, 28f, trackHeight);

            float t = maxScroll > 0f ? Mathf.Clamp01(-scroll / maxScroll) : 0f;

            scrollThumb.rectTransform.sizeDelta = new Vector2(ScrollBarWidth, thumbHeight);
            scrollThumb.rectTransform.anchoredPosition = new Vector2(0f, -t * (trackHeight - thumbHeight));
        }

        public void RefreshCounts()
        {
            if (pool == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                int remaining = pool.Remaining(row.type);
                int limit = pool.Limit(row.type);

                row.available = remaining > 0;
                row.countText.text = string.Format("{0} / {1}", remaining, limit);

                ApplyRowColors(row);
            }
        }

        private void ApplyRowColors(Row row)
        {
            if (row.background == null)
                return;

            Color color = row.available ? row.baseColor : Fade(row.baseColor, Palette.BlockDim.a + 0.25f);
            Color textColor = row.available ? row.baseTextColor : new Color(
                row.baseTextColor.r, row.baseTextColor.g, row.baseTextColor.b, 0.45f);

            if (row.hovered && row.available)
            {
                color = Color.Lerp(row.baseColor, Color.white, 0.22f);
                textColor = row.baseTextColor;
            }

            row.background.color = color;
            row.nameText.color = textColor;
            row.countText.color = textColor;
        }

        private static Color Fade(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        // ------------------------------------------------------------------
        // 滚动与命中
        // ------------------------------------------------------------------

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return root != null
                && root.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPoint, null);
        }

        /// <summary>命中测试：返回被点中的块类型。必须点在有剩余数量的行上。</summary>
        public bool TryHitRow(Vector2 screenPoint, out BlockType type)
        {
            type = BlockType.Start;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.rect == null)
                    continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(row.rect, screenPoint, null))
                {
                    type = row.type;
                    return row.available;
                }
            }

            return false;
        }

        public void Scroll(float amount)
        {
            if (viewport == null || content == null)
                return;

            float viewportHeight = viewport.rect.height;
            if (viewportHeight < 1f)
                viewportHeight = FallbackViewportHeight;

            float contentHeight = content.rect.height;
            float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);

            scroll = Mathf.Clamp(scroll - amount, -maxScroll, 0f);
            content.anchoredPosition = new Vector2(0f, scroll);

            UpdateScrollBar(maxScroll, viewportHeight);
        }

        public bool NeedsScrolling
        {
            get
            {
                return viewport != null && content != null
                    && content.rect.height > viewport.rect.height;
            }
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                return;

            // Game 视图尺寸变化时重新排版，保证「本关所有块都看得见」始终成立
            if (Mathf.Abs(viewport.rect.height - lastViewportHeight) > 0.5f)
                LayoutRows();

            UpdateHover();
        }

        /// <summary>鼠标悬停行时轻微提亮，提示「这一行可以拖出来」。</summary>
        private void UpdateHover()
        {
            var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

            // 必须先初始化：下面用 && 短路，鼠标不在库存面板上时 TryHitRow 不会执行，
            // 编译器无法证明 hit 已被赋值（CS0165）。（over 为 false 时不会读 hit。）
            BlockType hit = BlockType.Start;
            bool over = ContainsScreenPoint(mouse) && TryHitRow(mouse, out hit);

            if (hasHoveredType && (!over || hit != hoveredType))
            {
                SetHovered(hoveredType, false);
                hasHoveredType = false;
            }

            if (over && !hasHoveredType)
            {
                SetHovered(hit, true);
                hoveredType = hit;
                hasHoveredType = true;
            }
        }

        private void SetHovered(BlockType type, bool value)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].type != type)
                    continue;

                rows[i].hovered = value;
                ApplyRowColors(rows[i]);
                return;
            }
        }
    }
}
