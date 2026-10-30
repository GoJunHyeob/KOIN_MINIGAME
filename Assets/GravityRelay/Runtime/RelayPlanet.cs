using UnityEngine;

namespace GravityRelay
{
    [ExecuteAlways]
    public sealed class RelayPlanet : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("행성 반경. 표면 크기와 공전 가이드에 즉시 반영됩니다.")]
        private float radius = 0.8f;
        [SerializeField] private SpriteRenderer surface;
        [SerializeField] private SpriteRenderer orbitGuide;
        [SerializeField] private RelayPlayer orbitSource;
        public float Radius => radius;
        public Vector2 Center => transform.position;

        public void ConfigureVisuals(SpriteRenderer planetSurface, SpriteRenderer guide, RelayPlayer player)
        {
            surface = planetSurface;
            orbitGuide = guide;
            orbitSource = player;
            RefreshVisuals();
        }

        private void LateUpdate() => RefreshVisuals();

        public void RefreshVisuals()
        {
            SetDiameter(surface, radius * 2f);
            if (orbitSource != null)
                SetDiameter(orbitGuide, orbitSource.OrbitRadius(this) * 2f);
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
