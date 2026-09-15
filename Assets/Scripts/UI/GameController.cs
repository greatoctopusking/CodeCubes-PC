using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeCubesPC
{
    public enum GameState
    {
        Title = 0,
        LevelSelect = 1,
        InLevel = 2,
        Complete = 3,
        Failed = 4
    }

    /// <summary>
    /// 全局编排 + 鼠标输入路由。
    ///
    /// 输入映射（用户确认，对应 VR 手柄）：
    ///   左键拖动  ←→  VR 右握柄（抓取 / 移动块）
    ///   右键单击  ←→  VR 右扳机（选中 / 连线 / 断开）
    ///   滚轮      →   工作区缩放；在左下库存上则滚动列表
    ///   中键拖动  →   工作区平移
    ///
    /// 输入在这里统一做命中测试后分发给各视图，视图自身不注册 uGUI 指针事件，
    /// 避免自绘连线 / 遮罩 / 垃圾桶与块拖拽抢事件。
    /// 唯一例外是可点击的 Button（Run / Leave / 菜单），它们走 EventSystem，
    /// 通过 IsPointerOverGameObject() 让本路由器让行；
    /// 因此所有非按钮的 Image / Text 的 raycastTarget 一律为 false。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        private const float DragThresholdSqr = 36f;
        private const float ScrollStep = 48f;

        private BoardView board;
        private InventoryView inventory;
        private WorkspaceView workspace;
        private MenuView menu;
        private RectTransform gameRoot;

        private LevelManager levelManager;
        private ConnectionManager connections;
        private CodeExecutor executor;
        private readonly BlockPool pool = new BlockPool();

        private GameState state = GameState.Title;

        private enum DragMode
        {
            None = 0,
            PendingSpawn = 1,
            DraggingBlock = 2
        }

        private DragMode dragMode = DragMode.None;
        private BlockType pendingType = BlockType.MoveForward;
        private Vector2 dragStartScreen;
        private BlockView draggedView;
        private bool draggedIsNew;
        private Vector2 grabOffset;
        private Vector2 dragOriginalPosition;

        private bool panning;
        private Vector2 lastPanContent;

        public GameState State { get { return state; } }

        /// <summary>唯一的 Start 块（对应原项目 FindProgramStart 的结果）。</summary>
        public CodeBlock StartBlock { get { return workspace != null ? workspace.StartBlock : null; } }

        /// <summary>只有「关卡内且未结算」时才处理游戏区输入。</summary>
        public bool IsGameInputEnabled
        {
            get { return state == GameState.InLevel && levelManager != null && levelManager.IsLevelActive; }
        }

        // ------------------------------------------------------------------
        // 启动
        // ------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;

            levelManager = gameObject.AddComponent<LevelManager>();
            connections = gameObject.AddComponent<ConnectionManager>();
            executor = gameObject.AddComponent<CodeExecutor>();

            connections.Init(executor);
            executor.Init(connections);

            levelManager.RunResolved += OnRunResolved;

            BuildUi();
            LogLevelValidation();

            ShowTitle();
        }

        private void OnDestroy()
        {
            if (levelManager != null)
                levelManager.RunResolved -= OnRunResolved;

            if (Instance == this)
                Instance = null;
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("CodeCubesCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasObject.GetComponent<RectTransform>();

            // 全屏底色
            var background = UIFactory.CreatePanel("ScreenBackground", canvasRect, Palette.ScreenBg, false, false);
            UIFactory.Stretch(background.rectTransform);

            // 游戏区（上半棋盘 + 下半库存 / 工作区），菜单状态下整体隐藏
            gameRoot = UIFactory.CreateRect("GameRoot", canvasRect);
            UIFactory.Stretch(gameRoot);

            board = gameRoot.gameObject.AddComponent<BoardView>();
            board.Build(gameRoot);

            inventory = gameRoot.gameObject.AddComponent<InventoryView>();
            inventory.Build(gameRoot);
            inventory.Init(pool);

            workspace = gameRoot.gameObject.AddComponent<WorkspaceView>();
            workspace.Build(gameRoot);
            workspace.Init(connections, pool);

            // 菜单在游戏区之后创建，层级更高，遮罩能盖住游戏区
            menu = canvasRect.gameObject.AddComponent<MenuView>();
            menu.Build(canvasRect, this);
        }

        private void LogLevelValidation()
        {
            var problems = LevelLibrary.ValidateAll();

            if (problems.Count == 0)
            {
                Debug.Log(string.Format(
                    "[CodeCubesPC] 关卡自检通过：{0} 关，坐标 / 星星 / 可用块全部合法。",
                    LevelLibrary.LevelCount));
                return;
            }

            for (int i = 0; i < problems.Count; i++)
                Debug.LogError("[CodeCubesPC] 关卡数据问题 -> " + problems[i]);
        }

        // ------------------------------------------------------------------
        // 状态切换
        // ------------------------------------------------------------------

        private void SetGameAreaVisible(bool visible)
        {
            if (gameRoot != null)
                gameRoot.gameObject.SetActive(visible);
        }

        public void ShowTitle()
        {
            state = GameState.Title;
            AbandonDrag();
            executor.StopExecution();
            levelManager.StopLevel();
            connections.CancelSelection();
            SetGameAreaVisible(false);
            menu.ShowTitle();
        }

        public void ShowLevelSelect()
        {
            state = GameState.LevelSelect;
            AbandonDrag();
            executor.StopExecution();
            levelManager.StopLevel();
            connections.CancelSelection();
            SetGameAreaVisible(false);
            menu.ShowLevelSelect();
        }

        public void SelectLevel(int index)
        {
            LoadLevel(index, false);
        }

        public void NextLevel()
        {
            int next = levelManager.CurrentLevelIndex + 1;
            if (next >= levelManager.LevelCount)
            {
                ShowLevelSelect();
                return;
            }

            LoadLevel(next, false);
        }

        /// <summary>Retry (Keep)：保留程序区与库存，只重置机器人和星星。</summary>
        public void RetryKeep()
        {
            LoadLevel(levelManager.CurrentLevelIndex, true);
        }

        /// <summary>Retry (Clear)：清空程序区（块全部归还），再重置本关。</summary>
        public void RetryClear()
        {
            LoadLevel(levelManager.CurrentLevelIndex, false);
        }

        public void LeaveLevel()
        {
            ShowLevelSelect();
        }

        /// <summary>
        /// 对应原 LevelManager.LoadLevelByIndex(index, keepWorkspace)。
        /// keepWorkspace=false 时：清空工作区、重置关卡、按本关重建库存、放置唯一的 Start。
        /// 同一关卡重试时重建库存得到的数量与原先一致，因此总量守恒不会被破坏。
        /// </summary>
        public void LoadLevel(int index, bool keepWorkspace)
        {
            if (levelManager == null)
                return;

            AbandonDrag();

            executor.StopExecution();
            connections.CancelSelection();
            executor.ClearAllHighlights();

            if (keepWorkspace)
            {
                if (!levelManager.TryLoad(index))
                    return;
            }
            else
            {
                workspace.ClearAllBlocks(false);

                if (!levelManager.TryLoad(index))
                    return;

                pool.BuildForLevel(levelManager.CurrentLevel.GetSuggestedBlockNames());
                workspace.PrepareNewLevel();
            }

            state = GameState.InLevel;
            SetGameAreaVisible(true);

            board.Refresh(levelManager.CurrentLevel);
            board.SetFacingIndicatorDimmed(false);
            inventory.Rebuild();
            menu.ShowInLevel();

            connections.CancelSelection();
        }

        private void OnRunResolved(bool success, string reason)
        {
            board.SetFacingIndicatorDimmed(false);

            if (success)
            {
                state = GameState.Complete;
                menu.ShowLevelComplete();
            }
            else
            {
                state = GameState.Failed;
                menu.ShowLevelFailed(reason);
            }
        }

        /// <summary>Run 按钮（对应原左扳机）。</summary>
        public void RequestRun()
        {
            executor.ToggleRun();
        }

        // ------------------------------------------------------------------
        // 状态文本（原 MenuManager.SetStatus / ClearStatus）
        // ------------------------------------------------------------------

        public void SetStatus(string message)
        {
            if (levelManager != null && levelManager.IsLevelActive && board != null)
                board.ShowError(message);
        }

        public void ClearStatus()
        {
            if (levelManager != null && levelManager.IsLevelActive && board != null)
                board.RestoreHints();
        }

        public void HideHints()
        {
            if (board != null)
                board.HideHints();
        }

        /// <summary>
        /// 原实现会在程序运行时隐藏朝向箭头（VR 里机器人模型自带朝向）。
        /// 2D 版机器人是圆加箭头，藏起来就完全看不出朝向了，
        /// 因此改成运行时变暗、结束后恢复，既保留原调用点又不丢信息。
        /// </summary>
        public void HideFacingIndicator()
        {
            if (board != null)
                board.SetFacingIndicatorDimmed(true);
        }

        public void ShowFacingIndicator()
        {
            if (board != null)
                board.SetFacingIndicatorDimmed(false);
        }

        // ------------------------------------------------------------------
        // 输入
        // ------------------------------------------------------------------

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            var mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

            if (!IsGameInputEnabled)
            {
                AbandonDrag();
                panning = false;
                return;
            }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            HandleScroll(mouse, overUi);
            HandleMiddlePan(mouse, overUi);
            HandleRightClick(mouse, overUi);
            HandleLeftButton(mouse, overUi);
        }

        private void HandleScroll(Vector2 mouse, bool overUi)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            if (!overUi && workspace.ContainsScreenPoint(mouse))
            {
                workspace.ZoomAt(1f - scroll * 0.12f, mouse);
                return;
            }

            if (inventory.ContainsScreenPoint(mouse))
                inventory.Scroll(scroll * ScrollStep);
        }

        private void HandleMiddlePan(Vector2 mouse, bool overUi)
        {
            if (!panning && Input.GetMouseButtonDown(2) && !overUi && workspace.ContainsScreenPoint(mouse))
            {
                panning = true;
                lastPanContent = workspace.ScreenToContent(mouse);
            }

            if (!panning)
                return;

            if (Input.GetMouseButton(2))
            {
                Vector2 now = workspace.ScreenToContent(mouse);
                workspace.PanByContentDelta(now - lastPanContent);
                lastPanContent = now;
            }
            else
            {
                panning = false;
            }
        }

        private void HandleRightClick(Vector2 mouse, bool overUi)
        {
            if (!Input.GetMouseButtonDown(1) || overUi)
                return;

            if (workspace.ContainsScreenPoint(mouse))
            {
                BlockView hit = workspace.HitTestBlock(mouse);
                connections.HandleRightClick(hit != null ? hit.Block : null);
            }
            else
            {
                connections.CancelSelection();
            }
        }

        private void HandleLeftButton(Vector2 mouse, bool overUi)
        {
            if (Input.GetMouseButtonDown(0) && !overUi)
                BeginLeftPress(mouse);

            if (Input.GetMouseButton(0))
                ContinueLeftDrag(mouse);

            if (Input.GetMouseButtonUp(0))
                FinishLeftDrag(mouse);
        }

        private void BeginLeftPress(Vector2 mouse)
        {
            // a) 在库存行上按下：等拖拽超过阈值后再生成块
            if (inventory.ContainsScreenPoint(mouse))
            {
                BlockType type;
                if (inventory.TryHitRow(mouse, out type))
                {
                    dragMode = DragMode.PendingSpawn;
                    pendingType = type;
                    dragStartScreen = mouse;
                }
                return;
            }

            // b) 工作区里已有的块：开始拖动
            if (workspace.ContainsScreenPoint(mouse))
            {
                BlockView hit = workspace.HitTestBlock(mouse);
                if (hit == null)
                    return;

                dragMode = DragMode.DraggingBlock;
                draggedView = hit;
                draggedIsNew = false;
                dragOriginalPosition = hit.Position;
                grabOffset = hit.Position - workspace.ScreenToContent(mouse);
                workspace.BringToFront(hit);
            }
        }

        private void ContinueLeftDrag(Vector2 mouse)
        {
            if (dragMode == DragMode.PendingSpawn)
            {
                if ((mouse - dragStartScreen).sqrMagnitude > DragThresholdSqr)
                    BeginSpawnDrag(mouse);
                return;
            }

            if (dragMode != DragMode.DraggingBlock || draggedView == null)
                return;

            draggedView.SetPosition(workspace.ScreenToContent(mouse) + grabOffset);
            workspace.ClampBlock(draggedView);
            workspace.SetTrashHot(workspace.ContainsTrashPoint(mouse));
        }

        /// <summary>
        /// 从库存拖出一个新块：先扣库存（原项目抓取后槽位变空、不补货），再在工作区生成。
        /// </summary>
        private void BeginSpawnDrag(Vector2 mouse)
        {
            if (!pool.TryTake(pendingType))
            {
                ResetDragState();
                return;
            }

            draggedView = workspace.CreateBlock(pendingType, workspace.ScreenToContent(mouse));
            draggedIsNew = true;
            grabOffset = Vector2.zero;
            dragMode = DragMode.DraggingBlock;
        }

        private void FinishLeftDrag(Vector2 mouse)
        {
            if (dragMode != DragMode.DraggingBlock || draggedView == null)
            {
                ResetDragState();
                return;
            }

            bool overTrash = workspace.ContainsTrashPoint(mouse);
            bool inWorkspace = workspace.ContainsScreenPoint(mouse);
            bool executing = executor != null && executor.IsExecuting;

            if (overTrash)
            {
                if (executing)
                {
                    // 原 D3：程序运行中垃圾桶不可用，块留在原地
                    SnapBack();
                }
                else if (!workspace.ReturnBlock(draggedView))
                {
                    // 拒绝归还（唯一的 Start，或同类型槽位已满）：块不被销毁，回到原位
                    SnapBack();
                }
            }
            else if (!inWorkspace)
            {
                if (draggedIsNew)
                {
                    // 拖出程序区松手等于取消，退回库存
                    workspace.ReturnBlock(draggedView);
                }
                else
                {
                    SnapBack();
                }
            }
            else
            {
                workspace.ClampBlock(draggedView);
            }

            ResetDragState();
        }

        private void SnapBack()
        {
            if (draggedView == null)
                return;

            draggedView.SetPosition(dragOriginalPosition);
            workspace.ClampBlock(draggedView);
        }

        private void ResetDragState()
        {
            dragMode = DragMode.None;
            draggedView = null;
            draggedIsNew = false;
            if (workspace != null)
                workspace.SetTrashHot(false);
        }

        /// <summary>输入被禁用（结算面板弹出等）时，安全地放弃正在进行的拖拽。</summary>
        private void AbandonDrag()
        {
            if (dragMode == DragMode.DraggingBlock && draggedView != null)
            {
                if (draggedIsNew)
                    workspace.ReturnBlock(draggedView);
                else
                    SnapBack();
            }

            ResetDragState();
        }
    }
}
