using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 屏幕下半部分右侧：代码块工作区（自由摆放 + 滚轮缩放 + 中键平移）与垃圾桶。
    ///
    /// 坐标系统一为「内容局部坐标」：
    ///   * 块的 anchoredPosition、块的端口、连线顶点，全部用同一套坐标
    ///   * 屏幕坐标与内容坐标的换算一律走 ScreenToContent（基于
    ///     RectTransformUtility.ScreenPointToLocalPointInRectangle，自动处理缩放与平移）
    ///
    /// 交互全部由 GameController 统一命中测试后调用，本类不注册任何 uGUI 指针事件，
    /// 这样自绘的连线、遮罩、垃圾桶不会和块的拖拽 / 右键抢事件。
    /// </summary>
    public class WorkspaceView : MonoBehaviour
    {
        public const float ContentWidth = 3600f;
        public const float ContentHeight = 2400f;

        private const float LineWidthScreen = 4f;
        private const float LineWidthJudgerScreen = 4f;
        private const float PreviewWidthScreen = 3f;
        private const float MinZoom = 0.45f;
        private const float MaxZoom = 1.5f;
        private const float TrashWidth = 152f;
        private const float TrashHeight = 116f;

        /// <summary>唯一 Start 块的固定落点（对应原 PlaceUniqueStartOnGround）。</summary>
        private static readonly Vector2 StartPosition = new Vector2(-430f, 0f);

        private static readonly Vector2 ContentMin = new Vector2(-ContentWidth * 0.5f, -ContentHeight * 0.5f);
        private static readonly Vector2 ContentMax = new Vector2(ContentWidth * 0.5f, ContentHeight * 0.5f);

        private RectTransform root;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform trashRect;
        private Image trashImage;
        private Text trashLabel;
        private UILineGraphic lines;

        private readonly List<BlockView> blocks = new List<BlockView>();
        private BlockView startView;

        private float zoom = 1f;
        private bool trashHot;

        private ConnectionManager connections;
        private BlockPool pool;

        public RectTransform Root { get { return root; } }
        public List<BlockView> Blocks { get { return blocks; } }
        public float Zoom { get { return zoom; } }
        public BlockView StartBlockView { get { return startView; } }
        public bool IsVisible { get { return root != null && root.gameObject.activeSelf; } }

        // ------------------------------------------------------------------
        // 构建
        // ------------------------------------------------------------------

        public void Build(Transform parent)
        {
            Image panel = UIFactory.CreatePanel("WorkspacePanel", parent, Palette.PanelBgAlt, false, false);
            root = panel.rectTransform;
            UIFactory.Anchor(root, new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                new Vector2(InventoryView.PanelWidth + 8f, 8f), new Vector2(-8f, 0f));

            // 视口（裁切）
            viewport = UIFactory.CreateRect("Viewport", root);
            UIFactory.Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            // 可平移缩放的 content 根
            content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ContentWidth, ContentHeight);
            content.anchoredPosition = Vector2.zero;
            content.localScale = Vector3.one;

            // 连线层必须在块下面
            lines = UILineGraphic.Create("Lines", content);
            lines.transform.SetAsFirstSibling();

            BuildHelpText();
            BuildResetViewButton();
            BuildTrash();
        }

        private void BuildHelpText()
        {
            Text help = UIFactory.CreateText("Help", root,
                "Left-drag: move block     Right-click: connect / disconnect     Wheel: zoom     Middle-drag: pan",
                14, Palette.TextDim, TextAnchor.LowerLeft);
            UIFactory.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 12f), new Vector2(900f, 22f));
        }

        private void BuildResetViewButton()
        {
            Button resetView = UIFactory.CreateButton("ResetViewButton", root, "Reset View", 14,
                Palette.ButtonBgAlt, ResetView);
            UIFactory.Place(resetView.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -12f), new Vector2(104f, 30f));
        }

        private void BuildTrash()
        {
            Image trash = UIFactory.CreatePanel("TrashZone", root, Palette.TrashReady, true, false);
            trashRect = trash.rectTransform;
            UIFactory.Place(trashRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 16f), new Vector2(TrashWidth, TrashHeight));
            trashImage = trash;

            trashLabel = UIFactory.CreateText("TrashLabel", trashRect, "Trash\ndrag here", 15,
                Palette.TextMain, TextAnchor.MiddleCenter);
            UIFactory.Stretch(trashLabel.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            trashLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public void Init(ConnectionManager connectionManager, BlockPool blockPool)
        {
            connections = connectionManager;
            pool = blockPool;
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------
        // 关卡装载
        // ------------------------------------------------------------------

        /// <summary>
        /// 全新关卡：清空工作区（块直接销毁，因为库存马上会按新关卡重建）、复位视图、
        /// 放置唯一的 Start。
        /// </summary>
        public void PrepareNewLevel()
        {
            ClearAllBlocks(false);
            ResetView();
            PlaceStart();
        }

        /// <summary>把块限制在程序区内容范围内（拖动与吸附后都要调用）。</summary>
        public void ClampBlock(BlockView view)
        {
            if (view != null)
                view.ClampToBounds(ContentMin, ContentMax);
        }

        private void PlaceStart()
        {
            if (startView != null)
                return;

            startView = CreateBlock(BlockType.Start, StartPosition);
        }

        public void ClearAllBlocks(bool returnToPool)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                BlockView view = blocks[i];
                if (view == null)
                    continue;

                DetachBlock(view, returnToPool);
                Destroy(view.gameObject);
            }

            blocks.Clear();
            startView = null;

            if (connections != null)
                connections.ClearAll();
        }

        private void DetachBlock(BlockView view, bool returnToPool)
        {
            CodeBlock block = view != null ? view.Block : null;
            if (block == null)
                return;

            if (connections != null)
            {
                connections.CleanupBlock(block);
                connections.Unregister(block);
            }

            if (returnToPool && pool != null)
                pool.TryReturn(block.type);
        }

        // ------------------------------------------------------------------
        // 块的增删
        // ------------------------------------------------------------------

        public BlockView CreateBlock(BlockType type, Vector2 contentPosition)
        {
            BlockView view = BlockView.Create(content, type);
            view.SetPosition(contentPosition);
            view.ClampToBounds(ContentMin, ContentMax);

            var block = view.gameObject.AddComponent<CodeBlock>();
            block.type = type;
            view.BindBlock(block);

            blocks.Add(view);

            if (connections != null)
                connections.Register(block);

            view.transform.SetAsLastSibling();
            return view;
        }

        /// <summary>
        /// 把一个块归还库存并销毁。返回 false 表示拒绝归还
        /// （原 CodeBlockBoard.ReturnBlock：同类型槽位已满时不销毁该块）。
        /// 唯一的 Start 不在库存里，因此也会被拒绝。
        /// </summary>
        public bool ReturnBlock(BlockView view)
        {
            if (view == null || view.Block == null)
                return false;

            if (pool == null || !pool.TryReturn(view.Block.type))
                return false;

            DetachBlock(view, false);
            blocks.Remove(view);

            if (view == startView)
                startView = null;

            Destroy(view.gameObject);
            return true;
        }

        public void BringToFront(BlockView view)
        {
            if (view != null)
                view.transform.SetAsLastSibling();
        }

        public BlockView FindView(CodeBlock block)
        {
            if (block == null)
                return null;

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null && blocks[i].Block == block)
                    return blocks[i];
            }

            return null;
        }

        public CodeBlock StartBlock
        {
            get { return startView != null ? startView.Block : null; }
        }

        // ------------------------------------------------------------------
        // 命中测试
        // ------------------------------------------------------------------

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return root != null
                && root.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPoint, null);
        }

        public bool ContainsTrashPoint(Vector2 screenPoint)
        {
            return root != null
                && root.gameObject.activeInHierarchy
                && trashRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(trashRect, screenPoint, null);
        }

        /// <summary>返回最上层被命中的块（重叠时以层级更高者为准）。</summary>
        public BlockView HitTestBlock(Vector2 screenPoint)
        {
            if (ContainsTrashPoint(screenPoint))
                return null;

            BlockView best = null;
            int bestSibling = -1;

            for (int i = 0; i < blocks.Count; i++)
            {
                BlockView view = blocks[i];
                if (view == null || view.Rect == null)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(view.Rect, screenPoint, null))
                    continue;

                int sibling = view.Rect.GetSiblingIndex();
                if (sibling > bestSibling)
                {
                    bestSibling = sibling;
                    best = view;
                }
            }

            return best;
        }

        // ------------------------------------------------------------------
        // 坐标换算
        // ------------------------------------------------------------------

        public Vector2 ScreenToContent(Vector2 screenPoint)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPoint, null, out local);
            return local;
        }

        /// <summary>中键平移：contentDelta 为内容局部坐标下的位移。</summary>
        public void PanByContentDelta(Vector2 contentDelta)
        {
            if (content == null)
                return;

            content.anchoredPosition += contentDelta * zoom;
        }

        public void ZoomAt(float factor, Vector2 screenPoint)
        {
            if (content == null)
                return;

            float newZoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Abs(newZoom - zoom) < 0.0001f)
                return;

            // 以鼠标位置为锚点缩放：缩放前后光标下的那个内容点保持不动
            Vector2 before = ScreenToContent(screenPoint);
            zoom = newZoom;
            content.localScale = new Vector3(zoom, zoom, 1f);
            Vector2 after = ScreenToContent(screenPoint);

            content.anchoredPosition += (after - before) * zoom;
        }

        public void ResetView()
        {
            if (content == null)
                return;

            zoom = 1f;
            content.localScale = Vector3.one;
            content.anchoredPosition = Vector2.zero;
        }

        public void SetTrashHot(bool hot)
        {
            trashHot = hot;
        }

        // ------------------------------------------------------------------
        // 每帧绘制
        // ------------------------------------------------------------------

        private void Update()
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                return;

            RedrawLines();
            UpdateTrashVisual();
        }

        private void RedrawLines()
        {
            if (lines == null || connections == null)
                return;

            lines.BeginBatch();

            float zoomSafe = Mathf.Max(zoom, 0.01f);
            float nextWidth = LineWidthScreen / zoomSafe;
            float judgerWidth = LineWidthJudgerScreen / zoomSafe;
            float previewWidth = PreviewWidthScreen / zoomSafe;

            List<BlockConnection> list = connections.Connections;
            for (int i = 0; i < list.Count; i++)
            {
                BlockConnection connection = list[i];
                if (connection.from == null || connection.to == null)
                    continue;

                BlockView fromView = FindView(connection.from);
                BlockView toView = FindView(connection.to);
                if (fromView == null || toView == null)
                    continue;

                // 黄线（next）：起点块右端口到终点块左端口
                // 绿线（judger）：While / IF 的下端口到条件块的上端口
                Vector2 start = connection.isJudger ? fromView.CondSlot : fromView.OutputPort;
                Vector2 end = connection.isJudger ? toView.TopPort : toView.InputPort;
                Color color = connection.isJudger ? Palette.LineJudger : Palette.LineNext;
                float width = connection.isJudger ? judgerWidth : nextWidth;

                lines.AddSegment(start, end, width, color, true);
            }

            // 已选中块的预览线：从该块右端口指向光标
            CodeBlock selected = connections.SelectedBlock;
            if (selected != null)
            {
                BlockView selectedView = FindView(selected);
                if (selectedView != null)
                {
                    var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                    Vector2 target = ScreenToContent(mouse);
                    lines.AddSegment(selectedView.OutputPort, target, previewWidth, Palette.LinePreview, false);
                }
            }

            lines.EndBatch();
        }

        private void UpdateTrashVisual()
        {
            if (trashImage == null)
                return;

            CodeExecutor executor = CodeExecutor.Instance;
            bool executing = executor != null && executor.IsExecuting;

            if (executing)
            {
                trashImage.color = Palette.TrashDisabled;
                if (trashLabel != null) trashLabel.text = "Trash\n(disabled)";
            }
            else if (trashHot)
            {
                trashImage.color = Palette.TrashHot;
                if (trashLabel != null) trashLabel.text = "Release to\nreturn";
            }
            else
            {
                trashImage.color = Palette.TrashReady;
                if (trashLabel != null) trashLabel.text = "Trash\ndrag here";
            }
        }
    }
}
