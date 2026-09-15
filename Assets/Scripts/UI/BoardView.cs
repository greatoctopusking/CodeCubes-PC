using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 屏幕上半部分：顶栏（关卡名 / 星星计数 / Run / Leave）、10×10 棋盘、机器人、星星，
    /// 以及右侧的提示面板（对应原项目 LevelBlockHintDisplay：
    /// 平时显示 "Suggested Blocks"，校验失败时变成黄色报错文字）。
    ///
    /// 棋盘与机器人都用 uGUI 绘制；机器人位置与朝向每帧从 LevelManager 读取，
    /// 因此 LevelManager 里的插值移动会在这里自动呈现。
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public const float CellSize = 44f;

        private const float TopBarHeight = 56f;
        private const float HintsPanelWidth = 364f;
        private const float RobotBodyRatio = 0.62f;
        private const float ArrowOffset = 24f;

        private RectTransform root;
        private RectTransform boardArea;
        private RectTransform gridRoot;
        private RectTransform robotRoot;
        private RectTransform robotArrow;
        private Image facingArrowImage;
        private Text levelNameText;
        private Text starCounterText;
        private Text hintText;
        private Button runButton;
        private Text runButtonLabel;

        private readonly List<Image> tiles = new List<Image>();
        private readonly List<Image> starImages = new List<Image>();

        private float originX;
        private float originY;
        private LevelData shownLevel;

        // ------------------------------------------------------------------
        // 构建
        // ------------------------------------------------------------------

        public void Build(Transform parent)
        {
            Image panel = UIFactory.CreatePanel("BoardPanel", parent, Palette.BoardBg, false, false);
            root = panel.rectTransform;
            UIFactory.Anchor(root, new Vector2(0f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            BuildTopBar();
            BuildBoardArea();
            BuildRobot();
            BuildHintsPanel();
        }

        private void BuildTopBar()
        {
            Image bar = UIFactory.CreatePanel("TopBar", root, Palette.TopBarBg, false, false);
            UIFactory.Anchor(bar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -TopBarHeight), Vector2.zero);

            levelNameText = UIFactory.CreateText("LevelName", bar.transform, "Level 1", 22,
                Palette.TextMain, TextAnchor.MiddleLeft);
            UIFactory.Place(levelNameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(300f, 30f));

            starCounterText = UIFactory.CreateText("StarCounter", bar.transform, "Stars  0 / 0", 20,
                Palette.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Place(starCounterText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(330f, 0f), new Vector2(260f, 30f));

            Button leave = UIFactory.CreateButton("LeaveButton", bar.transform, "Leave", 18,
                Palette.ButtonBgAlt, OnLeaveClicked);
            UIFactory.Place(leave.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f), new Vector2(110f, 38f));

            runButton = UIFactory.CreateButton("RunButton", bar.transform, "Run", 18,
                Palette.ButtonBg, OnRunClicked);
            UIFactory.Place(runButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-134f, 0f), new Vector2(110f, 38f));

            runButtonLabel = runButton.GetComponentInChildren<Text>();
        }

        private void BuildBoardArea()
        {
            boardArea = UIFactory.CreateRect("BoardArea", root);
            UIFactory.Anchor(boardArea, new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-(HintsPanelWidth + 16f), -TopBarHeight));

            gridRoot = UIFactory.CreateRect("Grid", boardArea);
        }

        private void BuildRobot()
        {
            robotRoot = UIFactory.CreateRect("Robot", gridRoot);
            UIFactory.Place(robotRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(CellSize * RobotBodyRatio, CellSize * RobotBodyRatio));

            Image body = UIFactory.CreatePanel("Body", robotRoot, Palette.Robot, false, false);
            body.sprite = SpriteFactory.Circle();
            UIFactory.Stretch(body.rectTransform);

            robotArrow = UIFactory.CreateRect("Facing", robotRoot);
            var arrow = robotArrow.gameObject.AddComponent<Image>();
            arrow.sprite = SpriteFactory.Triangle();
            arrow.color = Palette.FacingArrow;
            arrow.raycastTarget = false;
            facingArrowImage = arrow;

            UIFactory.Place(robotArrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(18f, 18f));
        }

        private void BuildHintsPanel()
        {
            Image panel = UIFactory.CreatePanel("HintsPanel", root, Palette.PanelBg, true, false);
            RectTransform rt = panel.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(HintsPanelWidth, -(TopBarHeight + 24f));
            rt.anchoredPosition = new Vector2(-12f, -(TopBarHeight * 0.5f));

            hintText = UIFactory.CreateText("HintText", rt, string.Empty, 18,
                Palette.TextMain, TextAnchor.UpperCenter);
            UIFactory.Stretch(hintText.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // ------------------------------------------------------------------
        // 关卡刷新
        // ------------------------------------------------------------------

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        public void Refresh(LevelData level)
        {
            shownLevel = level;

            Vector2Int size = level != null ? level.gridSize : new Vector2Int(10, 10);
            BuildGrid(size);
            BuildStars(level);

            if (robotRoot != null)
                robotRoot.SetAsLastSibling();

            if (levelNameText != null)
                levelNameText.text = level != null ? string.Format("Level {0}", level.levelNumber) : string.Empty;

            if (level != null)
                ShowHints(level);
            else
                HideHints();
        }

        private void BuildGrid(Vector2Int size)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != null)
                    Destroy(tiles[i].gameObject);
            }
            tiles.Clear();

            for (int i = 0; i < starImages.Count; i++)
            {
                if (starImages[i] != null)
                    Destroy(starImages[i].gameObject);
            }
            starImages.Clear();

            float width = size.x * CellSize;
            float height = size.y * CellSize;

            UIFactory.Place(gridRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(width, height));

            originX = -width * 0.5f;
            originY = -height * 0.5f;

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Color color = ((x + y) % 2 == 0) ? Palette.TileLight : Palette.TileDark;

                    Image tile = UIFactory.CreatePanel(string.Format("Tile_{0}_{1}", x, y), gridRoot,
                        color, false, false);
                    UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        CellToLocal(new Vector2Int(x, y)),
                        new Vector2(CellSize - 2f, CellSize - 2f));

                    tiles.Add(tile);
                }
            }
        }

        private void BuildStars(LevelData level)
        {
            if (level == null || level.starPositions == null)
                return;

            float size = CellSize * 0.62f;

            for (int i = 0; i < level.starPositions.Length; i++)
            {
                Image star = UIFactory.CreatePanel(string.Format("Star_{0}", i), gridRoot,
                    Palette.Star, false, false);
                star.sprite = SpriteFactory.Star();

                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    CellToLocal(level.starPositions[i]), new Vector2(size, size));

                starImages.Add(star);
            }
        }

        // ------------------------------------------------------------------
        // 坐标换算
        // ------------------------------------------------------------------

        private Vector2 CellToLocal(Vector2Int cell)
        {
            return new Vector2(originX + (cell.x + 0.5f) * CellSize,
                               originY + (cell.y + 0.5f) * CellSize);
        }

        /// <summary>格单位坐标（格 (0,0) 中心 = (0.5,0.5)）到棋盘局部坐标。</summary>
        private Vector2 PosToLocal(Vector2 pos)
        {
            return new Vector2(originX + pos.x * CellSize, originY + pos.y * CellSize);
        }

        // ------------------------------------------------------------------
        // 每帧同步
        // ------------------------------------------------------------------

        private void Update()
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                return;

            LevelManager level = LevelManager.Instance;
            if (level == null || robotRoot == null)
                return;

            SyncRobot(level);
            SyncStars(level);
            SyncCounter(level);
            SyncRunButton(level);
        }

        private void SyncRobot(LevelManager level)
        {
            robotRoot.anchoredPosition = PosToLocal(level.RobotPos);

            Vector2Int direction = RobotDirections.ToVector(level.RobotFacing);
            robotArrow.anchoredPosition = new Vector2(direction.x, direction.y) * ArrowOffset;
            robotArrow.localEulerAngles = new Vector3(0f, 0f, RobotDirections.ToUiAngle(level.RobotFacing));
        }

        private void SyncStars(LevelManager level)
        {
            List<StarState> stars = level.Stars;
            for (int i = 0; i < starImages.Count && i < stars.Count; i++)
            {
                if (starImages[i] == null)
                    continue;

                bool visible = !stars[i].collected;
                if (starImages[i].gameObject.activeSelf != visible)
                    starImages[i].gameObject.SetActive(visible);
            }
        }

        private void SyncCounter(LevelManager level)
        {
            if (starCounterText == null)
                return;

            starCounterText.text = string.Format("Stars  {0} / {1}", level.CollectedStarCount, level.StarCount);
        }

        private void SyncRunButton(LevelManager level)
        {
            if (runButton == null)
                return;

            CodeExecutor executor = CodeExecutor.Instance;
            bool executing = executor != null && executor.IsExecuting;
            bool usedUp = level.HasStartedThisAttempt || level.IsLevelResolved;

            runButton.interactable = !executing && !usedUp;

            if (runButtonLabel != null)
                runButtonLabel.text = executing ? "Running..." : "Run";
        }

        // ------------------------------------------------------------------
        // 提示面板（对应原 LevelBlockHintDisplay）
        // ------------------------------------------------------------------

        public void ShowHints(LevelData data)
        {
            if (data == null)
                return;

            shownLevel = data;
            ShowHintList();
        }

        public void ShowError(string message)
        {
            if (hintText == null)
                return;

            hintText.color = Palette.TextError;
            hintText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
            hintText.gameObject.SetActive(true);
        }

        public void RestoreHints()
        {
            if (shownLevel != null)
                ShowHintList();
            else
                HideHints();
        }

        public void HideHints()
        {
            if (hintText != null)
                hintText.gameObject.SetActive(false);
        }

        private void ShowHintList()
        {
            if (hintText == null || shownLevel == null)
                return;

            hintText.color = Palette.TextMain;
            hintText.text = FormatBlockList(shownLevel.GetSuggestedBlockNames());
            hintText.gameObject.SetActive(true);
        }

        /// <summary>与原 LevelBlockHintDisplay.FormatBlockList 的文案格式一致。</summary>
        private static string FormatBlockList(BlockType[] names)
        {
            if (names == null || names.Length == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
                parts.Add(BlockTypes.DisplayName(names[i]));

            return "Suggested Blocks\n\n" + string.Join("   ·   ", parts.ToArray());
        }

        // ------------------------------------------------------------------
        // 朝向指示箭头（原 RobotFacingIndicator）
        // ------------------------------------------------------------------

        /// <summary>
        /// 原项目在程序开始运行时隐藏朝向箭头（VR 里 3D 机器人模型本身就能看出朝向）。
        /// 2D 版机器人只是一个圆加上这个箭头，藏掉箭头就完全看不出朝向了，
        /// 那会直接破坏「转向」这个核心概念的教学，因此改为变暗但保留，运行结束时恢复。
        /// </summary>
        public void SetFacingIndicatorDimmed(bool dimmed)
        {
            if (facingArrowImage == null)
                return;

            facingArrowImage.color = dimmed
                ? new Color(Palette.FacingArrow.r, Palette.FacingArrow.g, Palette.FacingArrow.b, 0.25f)
                : Palette.FacingArrow;
        }

        // ------------------------------------------------------------------

        private static void OnRunClicked()
        {
            if (GameController.Instance != null)
                GameController.Instance.RequestRun();
        }

        private static void OnLeaveClicked()
        {
            if (GameController.Instance != null)
                GameController.Instance.LeaveLevel();
        }
    }
}
