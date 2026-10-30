using UnityEngine;

namespace GravityRelay
{
    // Three reused tiles cover the camera; alternating reflection joins identical edge pixels.
    [ExecuteAlways]
    public sealed class RelaySpaceBackground : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] tiles = new SpriteRenderer[3];
        [SerializeField] private Camera playCamera;
        [SerializeField] private Sprite artwork;
        [SerializeField, Range(0f, 1f)] private float parallax = 0.12f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.9f;
        private float originY;
        private Color flashColor;
        private float flashAmount;

        private void OnEnable()
        {
            if (playCamera != null) originY = playCamera.transform.position.y;
        }

        public void Configure(Camera camera, Sprite sprite, Material material, float intensity, float scroll)
        {
            playCamera = camera;
            artwork = sprite;
            brightness = Mathf.Clamp01(intensity);
            parallax = Mathf.Clamp01(scroll);
            originY = camera.transform.position.y;
            if (tiles == null || tiles.Length != 3) tiles = new SpriteRenderer[3];
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                {
                    var tile = new GameObject("Space background tile " + i, typeof(SpriteRenderer));
                    tile.transform.SetParent(transform, false);
                    tiles[i] = tile.GetComponent<SpriteRenderer>();
                }
                tiles[i].sprite = artwork;
                tiles[i].sharedMaterial = material;
                tiles[i].sortingOrder = -100;
            }
            LateUpdate();
        }

        public void SetFlash(Color color, float amount)
        {
            flashColor = color;
            flashAmount = Mathf.Clamp01(amount);
        }

        private void LateUpdate()
        {
            if (playCamera == null || artwork == null || tiles == null) return;
            Vector2 size = artwork.bounds.size;
            float height = playCamera.orthographicSize * 2f;
            float width = height * playCamera.aspect;
            // Slight overscan avoids a subpixel gap at the left/right viewport edges.
            float scale = Mathf.Max(width / size.x, height / size.y) * 1.002f;
            float tileHeight = size.y * scale;
            Vector3 cameraPoint = playCamera.transform.position;
            float offset = (cameraPoint.y - originY) * parallax;
            int middle = Mathf.FloorToInt(offset / tileHeight);
            Color tint = Color.Lerp(new Color(brightness, brightness, brightness, 1f), flashColor, flashAmount);
            tint.a = 1f;
            for (int i = 0; i < tiles.Length; i++)
            {
                int index = middle + i - 1;
                SpriteRenderer tile = tiles[i];
                if (tile == null) continue;
                if (!tile.enabled) tile.enabled = true;
                bool flipped = (index & 1) != 0;
                if (tile.flipY != flipped) tile.flipY = flipped;
                if (tile.color != tint) tile.color = tint;
                Vector3 tileScale = new Vector3(scale, scale, 1f);
                if (tile.transform.localScale != tileScale) tile.transform.localScale = tileScale;
                Vector3 tilePosition = new Vector3(cameraPoint.x,
                    cameraPoint.y + index * tileHeight - offset, cameraPoint.z + 5f);
                if (tile.transform.position != tilePosition) tile.transform.position = tilePosition;
            }
        }

        private void OnDisable()
        {
            if (tiles == null) return;
            foreach (SpriteRenderer tile in tiles)
                if (tile != null) tile.enabled = false;
        }
    }
}
