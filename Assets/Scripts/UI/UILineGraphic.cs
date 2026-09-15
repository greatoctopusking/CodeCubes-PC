using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodeCubesPC
{
    /// <summary>
    /// 在一个 MaskableGraphic 上批量绘制带箭头和线宽的折线段。
    /// 用于画代码块的连线（黄线=next，绿线=judger，蓝线=选中预览）。
    ///
    /// 顶点坐标使用**父级 RectTransform 的局部坐标**（相对父级 pivot）。
    /// 因此本组件必须被拉伸填满父级、且自身 pivot 为 (0.5,0.5)，
    /// 这样传给 VertexHelper 的坐标就等于父级的局部坐标。
    /// </summary>
    /// <remarks>
    /// [RequireComponent(typeof(CanvasRenderer))] 必不可少，不要删：
    /// uGUI 里 Graphic 基类只声明了 RequireComponent(typeof(RectTransform))，
    /// CanvasRenderer 是由「具体叶子类」各自声明的（Image / RawImage / Text 都有）。
    /// 自绘 Graphic 若不声明，AddComponent 时不会附带 CanvasRenderer，
    /// 于是 RectMask2D 裁切时会抛
    /// MissingComponentException: There is no 'CanvasRenderer' attached to ...
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineGraphic : MaskableGraphic
    {
        private struct Segment
        {
            public Vector2 a;
            public Vector2 b;
            public float halfWidth;
            public Color color;
            public bool arrowAtEnd;
        }

        private readonly List<Segment> segments = new List<Segment>();

        public static UILineGraphic Create(string name, Transform parent)
        {
            RectTransform rt = UIFactory.CreateRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var graphic = rt.gameObject.AddComponent<UILineGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        public void BeginBatch()
        {
            segments.Clear();
        }

        public void AddSegment(Vector2 from, Vector2 to, float width, Color color, bool arrowAtEnd)
        {
            var segment = new Segment();
            segment.a = from;
            segment.b = to;
            segment.halfWidth = Mathf.Max(0.5f, width * 0.5f);
            segment.color = color;
            segment.arrowAtEnd = arrowAtEnd;
            segments.Add(segment);
        }

        public void EndBatch()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];

                Vector2 delta = segment.b - segment.a;
                float length = delta.magnitude;
                if (length < 0.01f)
                    continue;

                Vector2 direction = delta / length;
                var normal = new Vector2(-direction.y, direction.x);

                float headLength = 0f;
                float shaftLength = length;

                if (segment.arrowAtEnd)
                {
                    headLength = Mathf.Min(segment.halfWidth * 5f, length * 0.45f);
                    shaftLength = length - headLength;
                }

                Vector2 shaftEnd = segment.a + direction * shaftLength;

                AddQuad(vh,
                    segment.a + normal * segment.halfWidth,
                    shaftEnd + normal * segment.halfWidth,
                    shaftEnd - normal * segment.halfWidth,
                    segment.a - normal * segment.halfWidth,
                    segment.color);

                if (segment.arrowAtEnd && headLength > 0.01f)
                {
                    float wingHalf = segment.halfWidth * 2.8f;
                    Vector2 tip = segment.a + direction * length;
                    Vector2 back = segment.a + direction * shaftLength;

                    AddTriangle(vh, tip,
                        back + normal * wingHalf,
                        back - normal * wingHalf,
                        segment.color);
                }
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(p0, color, Vector2.zero);
            vh.AddVert(p1, color, Vector2.zero);
            vh.AddVert(p2, color, Vector2.zero);
            vh.AddVert(p3, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTriangle(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(p0, color, Vector2.zero);
            vh.AddVert(p1, color, Vector2.zero);
            vh.AddVert(p2, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
