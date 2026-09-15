using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 用代码生成全部占位图形（不依赖任何外部美术资源）。
    /// 所有 sprite 都是**白色**的 alpha 形状，实际颜色由 Image.color 着色，
    /// 因此一个 sprite 可服务所有配色。
    ///
    /// 圆角矩形使用 9 宫格（border），因此可以任意拉伸而不失真：
    ///   Image.type = Image.Type.Sliced
    ///   Canvas.referencePixelsPerUnit 与 sprite 的 pixelsPerUnit 都是 100，
    ///   所以 border 的像素值就等于 UI 像素值。
    /// </summary>
    public static class SpriteFactory
    {
        /// <summary>圆角半径（像素）。所有圆角元素共用这一个 sprite。</summary>
        public const int CornerRadius = 10;

        private static Sprite solid;
        private static Sprite rounded;
        private static Sprite circle;
        private static Sprite star;
        private static Sprite triangle;

        /// <summary>纯白方块，用于面板/分隔线。</summary>
        public static Sprite Solid()
        {
            if (solid == null)
            {
                var pixels = new Color32[4];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);

                Texture2D tex = BuildTexture(2, 2, pixels);
                solid = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
                solid.name = "cc_solid";
            }
            return solid;
        }

        /// <summary>圆角矩形（9 宫格，可任意拉伸）。</summary>
        public static Sprite Rounded()
        {
            if (rounded == null)
            {
                int r = CornerRadius;
                int size = r * 2 + 4;          // 中间留 2px 的纯白带，保证 9 宫格拉伸不失真
                int border = r + 1;

                var pixels = new Color32[size * size];
                float half = size * 0.5f;
                float straight = half - r;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // 圆角矩形的有符号距离场
                        float px = x + 0.5f - half;
                        float py = y + 0.5f - half;
                        float dx = Mathf.Max(Mathf.Abs(px) - straight, 0f);
                        float dy = Mathf.Max(Mathf.Abs(py) - straight, 0f);
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) - r;

                        float alpha = Mathf.Clamp01(0.5f - dist);
                        pixels[y * size + x] = ToColor32(alpha);
                    }
                }

                Texture2D tex = BuildTexture(size, size, pixels);
                rounded = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                rounded.name = "cc_rounded";
            }
            return rounded;
        }

        public static Sprite Circle()
        {
            if (circle == null)
            {
                const int size = 64;
                var pixels = new Color32[size * size];
                float half = size * 0.5f;
                float radius = half - 1f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f - half;
                        float py = y + 0.5f - half;
                        float dist = Mathf.Sqrt(px * px + py * py) - radius;
                        float alpha = Mathf.Clamp01(0.5f - dist);
                        pixels[y * size + x] = ToColor32(alpha);
                    }
                }

                Texture2D tex = BuildTexture(size, size, pixels);
                circle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                circle.name = "cc_circle";
            }
            return circle;
        }

        public static Sprite Star()
        {
            if (star == null)
            {
                const int size = 64;
                const int samples = 4;

                // 五角星：10 个顶点，外半径 / 内半径交替
                float outer = size * 0.5f - 1f;
                float inner = outer * 0.42f;
                var poly = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.Deg2Rad * (90f + i * 36f);
                    float radius = (i % 2 == 0) ? outer : inner;
                    poly[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                }

                var pixels = BuildPolygon(size, size, poly, samples);
                Texture2D tex = BuildTexture(size, size, pixels);
                star = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                star.name = "cc_star";
            }
            return star;
        }

        /// <summary>朝右的等腰三角形（机器人朝向箭头）。</summary>
        public static Sprite Triangle()
        {
            if (triangle == null)
            {
                const int size = 32;
                const int samples = 4;
                float inset = 1f;

                var poly = new[]
                {
                    new Vector2(inset, inset),
                    new Vector2(size - inset, size * 0.5f),
                    new Vector2(inset, size - inset)
                };

                // 三角形是左右不对称的，用包围盒居中
                var offset = new Vector2(-size * 0.5f, -size * 0.5f);
                for (int i = 0; i < poly.Length; i++)
                    poly[i] += offset;

                var pixels = BuildPolygon(size, size, poly, samples);
                Texture2D tex = BuildTexture(size, size, pixels);
                triangle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                triangle.name = "cc_triangle";
            }
            return triangle;
        }

        // ------------------------------------------------------------------

        private static Texture2D BuildTexture(int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "cc_tex";
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Color32 ToColor32(float alpha)
        {
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            return new Color32(255, 255, 255, a);
        }

        /// <summary>用超采样把多边形栅格化为 alpha 贴图。</summary>
        private static Color32[] BuildPolygon(int width, int height, Vector2[] polygon, int samples)
        {
            var pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            int total = samples * samples;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples - halfW;
                            float py = y + (sy + 0.5f) / samples - halfH;
                            if (PointInPolygon(polygon, px, py))
                                hits++;
                        }
                    }

                    pixels[y * width + x] = ToColor32(hits / (float)total);
                }
            }

            return pixels;
        }

        private static bool PointInPolygon(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            int count = polygon.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                if ((a.y > y) != (b.y > y))
                {
                    float t = (y - a.y) / (b.y - a.y);
                    float crossX = a.x + t * (b.x - a.x);
                    if (x < crossX)
                        inside = !inside;
                }
            }

            return inside;
        }
    }
}
