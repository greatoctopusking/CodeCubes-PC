using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 全套菜单流程，对应原项目 MenuManager 的五个状态面板：
    ///   Title → Level Select → In Level → Level Complete / Level Failed
    ///
    /// 关键文案与原版逐字一致：
    ///   "Coding Blocks" / "Level N Complete!" / "Congratulations!\nYou've finished all the levels."
    ///   "Retry (Keep)" / "Retry (Clear)" / "Leave"
    ///
    /// 完成/失败面板用半透明遮罩，因此背后的棋盘与程序区仍然可见（便于复盘）。
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        private RectTransform root;
        private RectTransform titlePanel;
        private RectTransform levelSelectPanel;
        private RectTransform completePanel;
        private RectTransform failedPanel;

        private Text completeText;
        private Text failedTitleText;
        private Text failedReasonText;
        private Button nextButton;

        private GameController controller;

        public bool IsOverlayVisible
        {
            get
            {
                return IsActive(titlePanel) || IsActive(levelSelectPanel)
                    || IsActive(completePanel) || IsActive(failedPanel);
            }
        }

        private static bool IsActive(RectTransform panel)
        {
            return panel != null && panel.gameObject.activeSelf;
        }

        // ------------------------------------------------------------------

        public void Build(Transform parent, GameController gameController)
        {
            controller = gameController;

            root = UIFactory.CreateRect("MenuRoot", parent);
            UIFactory.Stretch(root);

            titlePanel = BuildTitle(root);
            levelSelectPanel = BuildLevelSelect(root);
            completePanel = BuildComplete(root);
            failedPanel = BuildFailed(root);

            HideAll();
        }

        private static RectTransform BuildOverlay(string name, Transform parent)
        {
            // 遮罩本身不接收射线，避免挡住下方控件；各按钮自己接收射线。
            Image background = UIFactory.CreatePanel(name, parent, Palette.PanelOverlay, false, false);
            UIFactory.Stretch(background.rectTransform);
            return background.rectTransform;
        }

        private RectTransform BuildTitle(Transform parent)
        {
            RectTransform panel = BuildOverlay("TitlePanel", parent);

            Text title = UIFactory.CreateText("Title", panel, "Coding Blocks", 72,
                Palette.TextMain, TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(1400f, 110f));

            Text subtitle = UIFactory.CreateText("Subtitle", panel,
                "Drag blocks from the left. Right-click to connect. Return blocks to the trash.",
                22, Palette.TextDim, TextAnchor.MiddleCenter);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(1400f, 40f));

            Button start = UIFactory.CreateButton("StartButton", panel, "Start", 26,
                Palette.ButtonBg, controller.ShowLevelSelect);
            UIFactory.Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(260f, 60f));

            // 首页退出入口：原版 VR 里摘头显即退出，2D 版需要一个显式按钮，
            // 否则全屏运行后只能靠 Alt+F4。样式与选关页的 Back 按钮一致（次要操作）。
            Button exit = UIFactory.CreateButton("ExitButton", panel, "Exit", 22,
                Palette.ButtonBgAlt, controller.QuitGame);
            UIFactory.Place(exit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f), new Vector2(200f, 50f));

            return panel;
        }

        private RectTransform BuildLevelSelect(Transform parent)
        {
            RectTransform panel = BuildOverlay("LevelSelectPanel", parent);

            Text title = UIFactory.CreateText("SelectTitle", panel, "Select a Level", 52,
                Palette.TextMain, TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 380f), new Vector2(900f, 70f));

            RectTransform container = UIFactory.CreateRect("LevelButtons", panel);
            UIFactory.Place(container, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(760f, 640f));

            const float buttonWidth = 240f;
            const float buttonHeight = 56f;
            const float gapX = 60f;
            const float gapY = 16f;
            const float topY = 280f;

            int count = LevelLibrary.LevelCount;
            int perColumn = (count + 1) / 2;

            for (int i = 0; i < count; i++)
            {
                int levelIndex = i;
                bool leftColumn = i < perColumn;
                int row = leftColumn ? i : i - perColumn;

                Button button = UIFactory.CreateButton(string.Format("LevelButton{0}", i + 1), container,
                    string.Format("Level {0}", i + 1), 20, Palette.ButtonBgAlt,
                    () => controller.SelectLevel(levelIndex));

                float x = (leftColumn ? -1f : 1f) * (buttonWidth + gapX) * 0.5f;
                float y = topY - row * (buttonHeight + gapY);

                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(buttonWidth, buttonHeight));
            }

            Button back = UIFactory.CreateButton("BackButton", panel, "Back", 20,
                Palette.ButtonBgAlt, controller.ShowTitle);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -420f), new Vector2(200f, 50f));

            return panel;
        }

        private RectTransform BuildComplete(Transform parent)
        {
            RectTransform panel = BuildOverlay("LevelCompletePanel", parent);

            completeText = UIFactory.CreateText("CompleteText", panel, string.Empty, 46,
                Palette.TextMain, TextAnchor.MiddleCenter);
            completeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(completeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(1100f, 220f));

            nextButton = UIFactory.CreateButton("NextButton", panel, "Next Level", 22,
                Palette.ButtonBg, controller.NextLevel);
            UIFactory.Place(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-140f, -90f), new Vector2(240f, 56f));

            Button select = UIFactory.CreateButton("CompleteSelectButton", panel, "Level Select", 22,
                Palette.ButtonBgAlt, controller.ShowLevelSelect);
            UIFactory.Place(select.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(140f, -90f), new Vector2(240f, 56f));

            return panel;
        }

        private RectTransform BuildFailed(Transform parent)
        {
            RectTransform panel = BuildOverlay("LevelFailedPanel", parent);

            failedTitleText = UIFactory.CreateText("FailedTitle", panel, "Level 1", 46,
                Palette.TextMain, TextAnchor.MiddleCenter);
            UIFactory.Place(failedTitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f), new Vector2(1100f, 70f));

            failedReasonText = UIFactory.CreateText("FailedReason", panel, string.Empty, 24,
                Palette.TextError, TextAnchor.UpperCenter);
            failedReasonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(failedReasonText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(1100f, 120f));

            Button retryKeep = UIFactory.CreateButton("RetryKeepButton", panel, "Retry (Keep)", 22,
                Palette.ButtonBg, controller.RetryKeep);
            UIFactory.Place(retryKeep.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f), new Vector2(320f, 56f));

            Button retryClear = UIFactory.CreateButton("RetryClearButton", panel, "Retry (Clear)", 22,
                Palette.ButtonBgAlt, controller.RetryClear);
            UIFactory.Place(retryClear.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -140f), new Vector2(320f, 56f));

            Button leave = UIFactory.CreateButton("FailedLeaveButton", panel, "Leave", 22,
                Palette.ButtonBgAlt, controller.LeaveLevel);
            UIFactory.Place(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -220f), new Vector2(220f, 56f));

            return panel;
        }

        // ------------------------------------------------------------------

        private void HideAll()
        {
            if (titlePanel != null) titlePanel.gameObject.SetActive(false);
            if (levelSelectPanel != null) levelSelectPanel.gameObject.SetActive(false);
            if (completePanel != null) completePanel.gameObject.SetActive(false);
            if (failedPanel != null) failedPanel.gameObject.SetActive(false);
        }

        public void ShowTitle()
        {
            HideAll();
            if (titlePanel != null) titlePanel.gameObject.SetActive(true);
        }

        public void ShowLevelSelect()
        {
            HideAll();
            if (levelSelectPanel != null) levelSelectPanel.gameObject.SetActive(true);
        }

        /// <summary>关卡内：不显示任何菜单面板（棋盘/HUD 由 BoardView 负责）。</summary>
        public void ShowInLevel()
        {
            HideAll();
        }

        public void ShowLevelComplete()
        {
            HideAll();
            if (completePanel != null) completePanel.gameObject.SetActive(true);

            LevelManager level = LevelManager.Instance;
            bool isLast = level != null && level.IsLastLevel;
            int number = level != null && level.CurrentLevel != null ? level.CurrentLevel.levelNumber : 0;

            if (completeText != null)
            {
                completeText.text = isLast
                    ? "Congratulations!\nYou've finished all the levels."
                    : string.Format("Level {0} Complete!", number);
            }

            if (nextButton != null)
                nextButton.gameObject.SetActive(!isLast);
        }

        public void ShowLevelFailed(string reason = null)
        {
            HideAll();
            if (failedPanel != null)
                failedPanel.gameObject.SetActive(true);

            LevelManager level = LevelManager.Instance;
            int number = level != null && level.CurrentLevel != null ? level.CurrentLevel.levelNumber : 0;

            if (failedTitleText != null)
                failedTitleText.text = string.Format("Level {0}", number);

            if (failedReasonText != null)
                failedReasonText.text = string.IsNullOrEmpty(reason) ? string.Empty : reason;
        }
    }
}
