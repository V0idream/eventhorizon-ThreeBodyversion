using UnityEngine;
using UnityEngine.UI;

namespace Gui.Controls
{
    [AddComponentMenu("UI/ProgressBar", 1000)]
    public class ProgressBar : Graphic
    {
        [SerializeField] private Sprite _image;
        [SerializeField] private float _aspectRatio = 1.0f;

        [Range(0.0f, 1.0f)] public float X0 = 0;
        [Range(0.0f, 1.0f)] public float X1 = 1;
        [Range(0.0f, 1.0f)] public float Y0 = 0;
        [Range(0.0f, 1.0f)] public float Y1 = 1;

        public int SegmentCount { get; set; }
        public float SegmentGapPixels { get; set; } = 2f;

        public override Texture mainTexture { get { return _image != null ? _image.texture : base.mainTexture; } }

        public void UseSolidTexture()
        {
            if (_image == null)
                return;

            _image = null;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            var rect = rectTransform.rect;

            vertexHelper.Clear();
            if (SegmentCount > 1 && rect.height > 0.01f)
            {
                PopulateSegmented(vertexHelper, rect);
                return;
            }

            var x0 = rect.xMin + X0*rect.width;
            var x1 = rect.xMin + X1*rect.width;
            var y0 = rect.yMax - Y0*rect.height;
            var y1 = rect.yMax - Y1*rect.height;
            
            var u = _image == null ? 1 : _aspectRatio*rect.width / _image.pixelsPerUnit;
            var v = _image == null ? 1 : _aspectRatio*rect.height / _image.pixelsPerUnit;

            var u0 = X0*u;
            var u1 = X1*u;
            var v0 = Y0*v;
            var v1 = Y1*v;

            AddQuad(vertexHelper, new Vector2(x0, y0), new Vector2(x1, y1),
                new Vector2(u0, v0), new Vector2(u1, v1));
        }

        private void PopulateSegmented(VertexHelper vertexHelper, Rect rect)
        {
            var count = Mathf.Max(2, SegmentCount);
            var gap = Mathf.Clamp(SegmentGapPixels / rect.height, 0f, 0.08f);
            var u = _image == null ? 1 : _aspectRatio * rect.width / _image.pixelsPerUnit;
            var v = _image == null ? 1 : _aspectRatio * rect.height / _image.pixelsPerUnit;

            for (var i = 0; i < count; ++i)
            {
                var segmentStart = i / (float)count;
                var segmentEnd = (i + 1) / (float)count;
                var start = Mathf.Max(Y0, segmentStart);
                var end = Mathf.Min(Y1, segmentEnd);
                if (end <= start)
                    continue;

                if (i > 0)
                    start = Mathf.Min(end, start + gap * 0.5f);
                if (i < count - 1)
                    end = Mathf.Max(start, end - gap * 0.5f);
                if (end <= start)
                    continue;

                var x0 = rect.xMin + X0 * rect.width;
                var x1 = rect.xMin + X1 * rect.width;
                var y0 = rect.yMax - start * rect.height;
                var y1 = rect.yMax - end * rect.height;
                AddQuad(vertexHelper, new Vector2(x0, y0), new Vector2(x1, y1),
                    new Vector2(X0 * u, start * v), new Vector2(X1 * u, end * v));
            }
        }

        private void AddQuad(VertexHelper vertexHelper, Vector2 topLeft, Vector2 bottomRight,
            Vector2 uvTopLeft, Vector2 uvBottomRight)
        {
            var index = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector2(topLeft.x, topLeft.y), color,
                new Vector2(uvTopLeft.x, uvTopLeft.y));
            vertexHelper.AddVert(new Vector2(bottomRight.x, topLeft.y), color,
                new Vector2(uvBottomRight.x, uvTopLeft.y));
            vertexHelper.AddVert(new Vector2(bottomRight.x, bottomRight.y), color,
                new Vector2(uvBottomRight.x, uvBottomRight.y));
            vertexHelper.AddVert(new Vector2(topLeft.x, bottomRight.y), color,
                new Vector2(uvTopLeft.x, uvBottomRight.y));
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index + 2, index + 3, index);
        }
    }
}
