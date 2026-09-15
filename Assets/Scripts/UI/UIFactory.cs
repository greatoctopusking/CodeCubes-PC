using System;
using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 纯代码构建 uGUI 的工厂方法。所有界面元素都由这里创建，
    /// 因此不需要在场景里手搭任何控件、也不依赖任何预制体。
    ///
    /// 约定：所有**背景/面板/文本**的 raycastTarget 一律为 false，
    /// 只有真正可点击的控件（按钮）才开启 —— 这样 InputRouter 里的
    /// EventSystem.IsPointerOverGameObject() 才能准确表示「鼠标是否压在控件上」。
    /// </summary>
    public static class UIFactory
    {
        private static Font cachedFont;

        /// <summary>
        /// 内置动态字体。Unity 2022.3 的内置字体资源名是 LegacyRuntime.ttf
        /// （旧版的 Arial.ttf 已被移除），因此这里按顺序回退，全部失败再退到系统字体。
        /// </summary>
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (cachedFont == null)
                        cachedFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 16);
                    if (cachedFont == null)
                        cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return cachedFont;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return rt;
        }

        public static Image CreatePanel(string name, Transform parent, Color color, bool rounded, bool raycast)
        {
            RectTransform rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = rounded ? SpriteFactory.Rounded() : SpriteFactory.Solid();
            image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            return CreatePanel(name, parent, color, false, false);
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize,
            Color color, TextAnchor anchor)
        {
            RectTransform rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, int fontSize,
            Color background, Action onClick)
        {
            Image image = CreatePanel(name, parent, background, true, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.50f, 0.50f, 0.50f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            Text text = CreateText("Label", image.transform, label, fontSize, Palette.TextMain,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            if (onClick != null)
            {
                Action callback = onClick;
                button.onClick.AddListener(() => callback());
            }

            return button;
        }

        /// <summary>子物体填满父物体。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>固定尺寸 + 单点锚定。</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        /// <summary>只设锚点范围与四边偏移（用于自适应布局）。</summary>
        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
