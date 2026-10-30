using UnityEngine;

namespace GravityRelay
{
    // Three reused tiles cover the camera; alternating reflection joins identical edge pixels.
    public sealed class RelaySpaceBackground : MonoBehaviour
    {
        private readonly SpriteRenderer[] tiles = new SpriteRenderer[3];
        private Camera playCamera;
        private Sprite artwork;
        private float parallax;
        private float brightness;
        private float originY;
        private Color flashColor;
        private float flashAmount;

        public void Configure(Camera camera, Sprite sprite, Material material, float intensity, float scroll)
        {
            playCamera = camera;
            artwork = sprite;
            brightness = Mathf.Clamp01(intensity);
            parallax = Mathf.Clamp01(scroll);
            originY = camera.transform.position.y;
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
            if (playCamera == null || artwork == null) return;
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
                tile.enabled = true;
                tile.flipY = (index & 1) != 0;
                tile.color = tint;
                tile.transform.localScale = new Vector3(scale, scale, 1f);
                tile.transform.position = new Vector3(cameraPoint.x,
                    cameraPoint.y + index * tileHeight - offset, cameraPoint.z + 5f);
            }
        }

        private void OnDisable()
        {
            foreach (SpriteRenderer tile in tiles)
                if (tile != null) tile.enabled = false;
        }
    }
}
