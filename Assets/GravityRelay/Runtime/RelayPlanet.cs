using UnityEngine;

namespace GravityRelay
{
    [ExecuteAlways]
    public sealed class RelayPlanet : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("행성 반경. 표면 크기와 공전 가이드에 즉시 반영됩니다.")]
        private float radius = 0.5f;
        [SerializeField] private SpriteRenderer surface;
        [SerializeField] private SpriteRenderer orbitGuide;
        [SerializeField] private RelayPlayer orbitSource;
        [SerializeField] private Transform crater;
        private float minimumLaunchWindow;
        private float captureAssist;
        public float Radius => radius;
        public float MinimumLaunchWindow => minimumLaunchWindow;
        public float CaptureAssist => captureAssist;
        public Vector2 Center => transform.position;

        public void SetLandingWindow(float seconds, float assist)
        {
            minimumLaunchWindow = Mathf.Max(0f, seconds);
            captureAssist = Mathf.Max(0f, assist);
        }

        public void ConfigureVisuals(SpriteRenderer planetSurface, SpriteRenderer guide, RelayPlayer player, Transform craterDetail = null)
        {
            surface = planetSurface;
            orbitGuide = guide;
            orbitSource = player;
            crater = craterDetail;
            RefreshVisuals();
        }

        private void LateUpdate() => RefreshVisuals();

        public void RefreshVisuals()
        {
            SetDiameter(surface, radius * 2f);
            if (crater != null)
            {
                Vector3 detailPosition = new Vector3(-radius * 0.25f, radius * 0.1875f, 0f);
                Vector3 detailScale = new Vector3(radius * 0.475f, radius * 0.475f, 1f);
                if (crater.localPosition != detailPosition) crater.localPosition = detailPosition;
                if (crater.localScale != detailScale) crater.localScale = detailScale;
            }
            if (orbitSource != null)
                SetDiameter(orbitGuide, orbitSource.OrbitRadius(this) * 2f);
            if (orbitGuide != null)
            {
                // Preserve authoring previews; during play reveal only the occupied orbit.
                bool visible = !Application.isPlaying || (orbitSource != null &&
                    orbitSource.State == RelayState.Orbiting && orbitSource.CurrentPlanet == this);
                if (orbitGuide.enabled != visible) orbitGuide.enabled = visible;
            }
        }

        private static void SetDiameter(SpriteRenderer renderer, float diameter)
        {
            if (renderer == null || renderer.sprite == null)
                return;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 scale = new Vector3(diameter / Mathf.Max(spriteSize.x, 0.001f),
                diameter / Mathf.Max(spriteSize.y, 0.001f), 1f);
            if (renderer.transform.localScale != scale)
                renderer.transform.localScale = scale;
        }
    }
}
