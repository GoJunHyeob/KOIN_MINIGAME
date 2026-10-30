using System.Collections.Generic;
using UnityEngine;

namespace GravityRelay
{
    public sealed class RelayEndlessCourse : MonoBehaviour
    {
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private RelayPlanet[] planetTemplates;
        [Header("Generation")]
        [SerializeField] private int seed = 2718;
        [SerializeField] private Vector2 startPosition = new Vector2(-1f, -3.2f);
        [SerializeField, Range(8, 12)] private int poolSize = 8;
        [SerializeField, Min(2.8f)] private float minVerticalGap = 3.1f;
        [SerializeField, Min(2.8f)] private float maxVerticalGap = 3.7f;
        [SerializeField, Range(0f, 2f)] private float horizontalLimit = 1.6f;
        [SerializeField, Min(0f)] private float separationMargin = 0.15f;
        [SerializeField, Range(1, 20)] private int candidateAttempts = 10;
        [Header("Camera and recycling")]
        [SerializeField, Min(0f)] private float cameraLookAhead = 3.2f;
        [SerializeField, Min(1f)] private float cameraSharpness = 12f;
        [SerializeField, Min(0f)] private float retireMargin = 1f;

        private readonly List<RelayPlanet> pool = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> active = new List<RelayPlanet>(12);
        private System.Random random;
        private RelayPlanet current;
        private RelayPlanet next;
        public RelayPlanet CurrentPlanet => current;
        public RelayPlanet TargetPlanet => next != null ? next : current;
        public int ActiveCount => active.Count;
        public int PoolCount => pool.Count;
        public IReadOnlyList<RelayPlanet> ActivePlanets => active;
        public bool ReadyForLaunch => next != null &&
            (playCamera.transform.position - FramedCameraPosition()).sqrMagnitude <= 0.0004f;
        public Rect FramedBounds => BoundsAt(FramedCameraPosition());

        public void Configure(RelayPlayer relayPlayer, Camera camera, RelayPlanet[] templates)
        {
            player = relayPlayer;
            playCamera = camera;
            planetTemplates = templates;
        }

        public bool ResetCourse()
        {
            if (planetTemplates == null || planetTemplates.Length < 2 || player == null || playCamera == null)
                return false;
            if (pool.Count == 0)
            {
                foreach (RelayPlanet planet in planetTemplates)
                    pool.Add(planet);
                while (pool.Count < Mathf.Clamp(poolSize, 8, 12))
                {
                    RelayPlanet copy = Instantiate(planetTemplates[pool.Count % planetTemplates.Length],
                        planetTemplates[0].transform.parent);
                    copy.name = "Pooled Relay Planet " + pool.Count;
                    pool.Add(copy);
                }
            }
            random = new System.Random(seed);
            foreach (RelayPlanet planet in pool)
                planet.gameObject.SetActive(false);
            active.Clear();
            current = pool[0];
            current.transform.position = startPosition;
            current.gameObject.SetActive(true);
            active.Add(current);
            next = null;
            playCamera.transform.position = FramedCameraPosition();
            return PrepareNext();
        }

        public bool Advance()
        {
            current = next;
            next = null;
            RetirePastPlanets();
            return PrepareNext();
        }

        public void Tick(float dt)
        {
            if (player.State == RelayState.Orbiting)
            {
                Vector3 destination = FramedCameraPosition();
                Vector3 point = Vector3.Lerp(playCamera.transform.position, destination,
                    1f - Mathf.Exp(-cameraSharpness * dt));
                if ((point - destination).sqrMagnitude <= 0.0004f)
                    point = destination;
                playCamera.transform.position = point;
            }
            RetirePastPlanets();
        }

        private Vector3 FramedCameraPosition() => new Vector3(0f,
            current != null ? current.Center.y + cameraLookAhead : startPosition.y + cameraLookAhead,
            playCamera.transform.position.z);

        private Rect BoundsAt(Vector3 point)
        {
            float height = playCamera.orthographicSize;
            float width = height * (9f / 16f);
            return Rect.MinMaxRect(point.x - width, point.y - height, point.x + width, point.y + height);
        }

        private void RetirePastPlanets()
        {
            float bottom = BoundsAt(playCamera.transform.position).yMin - retireMargin;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RelayPlanet planet = active[i];
                if (planet != current && planet != next &&
                    planet.Center.y + player.CaptureRadius(planet) < bottom)
                {
                    planet.gameObject.SetActive(false);
                    active.RemoveAt(i);
                }
            }
        }

        private bool PrepareNext()
        {
            RelayPlanet available = null;
            foreach (RelayPlanet planet in pool)
            {
                if (!planet.gameObject.activeSelf)
                {
                    available = planet;
                    break;
                }
            }
            if (available == null)
                return false;

            // Keep the familiar first jump; subsequent candidates stay above the current planet.
            if (player.Score == 0 && Accept(new Vector2(1f, 0f), available))
                return true;
            for (int attempt = 0; attempt < candidateAttempts; attempt++)
            {
                float x = Mathf.Lerp(-horizontalLimit, horizontalLimit, (float)random.NextDouble());
                float gap = Mathf.Lerp(minVerticalGap, Mathf.Max(minVerticalGap, maxVerticalGap),
                    (float)random.NextDouble());
                if (Accept(new Vector2(x, current.Center.y + gap), available))
                    return true;
            }
            // Bounded deterministic fallback, still tested with the real movement model.
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float x = Mathf.Clamp(attempt < 3 ? current.Center.x : attempt < 6 ? -0.8f : 0.8f,
                    -horizontalLimit, horizontalLimit);
                float gap = Mathf.Lerp(minVerticalGap, Mathf.Max(minVerticalGap, maxVerticalGap),
                    (attempt % 3) * 0.5f);
                if (Accept(new Vector2(x, current.Center.y + gap), available))
                    return true;
            }
            return false;
        }

        private bool Accept(Vector2 center, RelayPlanet candidate)
        {
            float gap = center.y - current.Center.y;
            if (gap < minVerticalGap || gap > Mathf.Max(minVerticalGap, maxVerticalGap) ||
                Mathf.Abs(center.x) > horizontalLimit)
                return false;
            foreach (RelayPlanet planet in active)
            {
                float distance = player.CaptureRadius(planet) + player.CaptureRadius(candidate) + separationMargin;
                if ((center - planet.Center).sqrMagnitude < distance * distance)
                    return false;
            }
            if (!player.TryFindLaunchWait(current, center, candidate.Radius, FramedBounds, out _))
                return false;
            candidate.transform.position = center;
            candidate.gameObject.SetActive(true);
            candidate.RefreshVisuals();
            active.Add(candidate);
            next = candidate;
            return true;
        }
    }
}
