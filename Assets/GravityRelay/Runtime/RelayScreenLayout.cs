using UnityEngine;

namespace GravityRelay
{
    public static class RelayScreenLayout
    {
        public const float Aspect = 9f / 16f;
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        // Screen coordinates are relative to the Unity Player window, including an embedded player.
        public static Rect Window => new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        public static Rect PlayArea => Fit(Window, Screen.safeArea);

        public static Rect Fit(Rect window, Rect safe)
        {
            float left = Mathf.Max(window.xMin, safe.xMin);
            float bottom = Mathf.Max(window.yMin, safe.yMin);
            float right = Mathf.Min(window.xMax, safe.xMax);
            float top = Mathf.Min(window.yMax, safe.yMax);
            Rect available = right > left && top > bottom
                ? Rect.MinMaxRect(left, bottom, right, top) : window;
            float width = Mathf.Min(available.width, available.height * Aspect);
            float height = width / Aspect;
            return new Rect(available.center.x - width * 0.5f, available.center.y - height * 0.5f, width, height);
        }

        public static void Apply(Camera camera)
        {
            if (camera == null) return;
            Rect window = Window;
            Rect area = PlayArea;
            Rect viewport = new Rect(area.x / window.width, area.y / window.height,
                area.width / window.width, area.height / window.height);
            if (camera.rect != viewport) camera.rect = viewport;
            if (!Mathf.Approximately(camera.aspect, Aspect)) camera.aspect = Aspect;
        }

        public static Rect WorldBounds(Camera camera, Vector3 center)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * Aspect;
            return Rect.MinMaxRect(center.x - halfWidth, center.y - halfHeight,
                center.x + halfWidth, center.y + halfHeight);
        }
    }
}
