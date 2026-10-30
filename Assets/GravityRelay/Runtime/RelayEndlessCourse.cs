using System.Collections.Generic;
using UnityEngine;

namespace GravityRelay
{
    public enum RelayDifficultyStage { Tutorial, Beginner, Normal, Difficult, Advanced, Endless }

    public sealed class RelayEndlessCourse : MonoBehaviour
    {
        [SerializeField] private RelayPlayer player;
        [SerializeField] private Camera playCamera;
        [SerializeField] private RelayPlanet[] planetTemplates;
        [Header("Generation")]
        [SerializeField] private int seed = 2718;
        [SerializeField] private Vector2 startPosition = new Vector2(-1f, -3.2f);
        [SerializeField, Range(12, 16)] private int poolSize = 12;
        [SerializeField, Range(4, 6), Tooltip("항상 앞쪽에 미리 배치할 행성 수입니다.")]
        private int planetsAhead = 4;
        [SerializeField, Min(2.8f), Tooltip("전체 생성 높이 차의 하한입니다.")]
        private float minVerticalGap = 5.6f;
        [SerializeField, Min(2.8f), Tooltip("난이도가 올라가도 넘지 않는 높이 차 상한입니다.")]
        private float maxVerticalGap = 6.6f;
        [SerializeField, Range(0f, 2.5f), Tooltip("모든 단계에서 유지되는 절대 좌우 생성 범위입니다.")]
        private float horizontalLimit = 2.2f;
        [SerializeField, Min(0f), Tooltip("수직으로 붙는 배치를 피하기 위한 최소 좌우 이동폭입니다.")]
        private float minimumHorizontalStep = 1.2f;
        [SerializeField, Min(0f)] private float separationMargin = 0.15f;
        [SerializeField, Range(1, 20)] private int candidateAttempts = 10;
        [Header("Progressive difficulty: placement only")]
        [SerializeField] private bool progressiveDifficulty = true;
        [SerializeField, Min(0), Tooltip("처음에 쉬운 거리·좌우 배치를 유지할 이동 횟수입니다.")]
        private int easyLandings = 3;
        [SerializeField, Min(1), Tooltip("이 점수부터는 생성 난이도를 더 높이지 않습니다.")]
        private int fullDifficultyScore = 15;
        [SerializeField, Min(2.8f)] private float earlyMinVerticalGap = 5.6f;
        [SerializeField, Min(2.8f)] private float earlyMaxVerticalGap = 5.9f;
        [SerializeField, Min(2.8f)] private float lateMinVerticalGap = 6.1f;
        [SerializeField, Min(0f), Tooltip("초반 목표와 현재 행성 사이 x 차이의 최대값입니다.")]
        private float earlyHorizontalStep = 2.6f;
        [SerializeField, Min(0f), Tooltip("후반 x 차이의 최대값. 절대 좌우 범위도 함께 적용됩니다.")]
        private float lateHorizontalStep = 3.6f;
        [Header("Minimum continuous launch window (seconds)")]
        [SerializeField, Min(0.01f)] private float tutorialWindow = 0.42f;
        [SerializeField, Min(0.01f)] private float beginnerWindow = 0.36f;
        [SerializeField, Min(0.01f)] private float normalWindow = 0.30f;
        [SerializeField, Min(0.01f)] private float difficultWindow = 0.26f;
        [SerializeField, Min(0.01f)] private float advancedWindow = 0.24f;
        [SerializeField, Min(0.01f)] private float endlessWindow = 0.24f;
        [SerializeField, Range(0.08f, 0.30f), Tooltip("원하는 성공 구간을 확보하지 못했을 때만 사용하는 최소 구간. 실제 도달·비겹침 판정은 유지합니다.")]
        private float fallbackLaunchWindow = 0.20f;
        [Header("Per-target capture assistance")]
        [SerializeField, Range(0f, 0.8f)] private float tutorialCaptureAssist = 0.55f;
        [SerializeField, Range(0f, 0.8f)] private float beginnerCaptureAssist = 0.30f;
        [SerializeField, Range(0f, 0.8f)] private float normalCaptureAssist = 0.10f;
        [SerializeField, Range(0f, 0.8f), Tooltip("구간 확보를 위한 목표별 추가 포획 여유의 상한입니다.")]
        private float maximumCaptureAssist = 0.8f;
        [Header("Flowing relay placement")]
        [SerializeField, Range(20f, 90f), Tooltip("이전 이동 방향과 다음 방향 사이 최대 각도. 급격한 지그재그를 줄입니다.")]
        private float maximumRelayTurn = 60f;
        [SerializeField, Range(0f, 1f), Tooltip("직전 좌우 흐름을 이어가는 후보를 우선할 확률입니다. 화면 끝에서는 안쪽으로 꺾습니다.")]
        private float directionPersistence = 0.85f;
        [SerializeField, Tooltip("착륙 시 다음 목표의 성공 구간에 더 빨리 도달하는 공전 방향을 선택합니다.")]
        private bool preferQuickDeparture = true;
        [Header("Camera and recycling")]
        [SerializeField, Min(0f)] private float cameraLookAhead = 3.6f;
        [SerializeField, Min(1f)] private float cameraSharpness = 28f;
        [SerializeField, Min(0f)] private float retireMargin = 1f;

        private readonly List<RelayPlanet> pool = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> active = new List<RelayPlanet>(12);
        private readonly List<RelayPlanet> upcoming = new List<RelayPlanet>(6);
        private System.Random random;
        private RelayPlanet current;
        private RelayPlanet next;
        private float generationMinGap;
        private float generationMaxGap;
        private float generationMaxStep;
        private float generationWindow;
        private float generationCaptureAssist;
        public RelayPlanet CurrentPlanet => current;
        public RelayPlanet TargetPlanet => next != null ? next : current;
        public int ConfiguredAheadCount => Mathf.Clamp(planetsAhead, 4, 6);
        public bool ReadyForLaunch => next != null &&
            (playCamera.transform.position - FramedCameraPosition()).sqrMagnitude <= 0.0004f;
        public Rect FramedBounds => BoundsAt(FramedCameraPosition());
        public int OpeningLandings => Mathf.Max(0, easyLandings);
        public int FullDifficultyAtScore => Mathf.Max(OpeningLandings + 1, fullDifficultyScore);
        public int CurrentScore => player != null ? player.Score : 0;
        public float AbsoluteHorizontalLimit => Mathf.Clamp(horizontalLimit, 0f, 2.5f);
        public float MinimumHorizontalStep(float maximumStep) => Mathf.Clamp(minimumHorizontalStep, 0f, maximumStep);
        public float HardMinimumGap => Mathf.Max(2.8f, minVerticalGap);
        public float HardMaximumGap => Mathf.Max(HardMinimumGap, maxVerticalGap);

        public float DifficultyProgressAt(int score) => Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(Mathf.Max(0, OpeningLandings - 1), FullDifficultyAtScore, Mathf.Max(0, score)));

        public RelayDifficultyStage StageAtScore(int score)
        {
            int move = Mathf.Max(0, score) + 1;
            if (move == 1) return RelayDifficultyStage.Tutorial;
            if (move <= 4) return RelayDifficultyStage.Beginner;
            if (move <= 8) return RelayDifficultyStage.Normal;
            if (move <= 15) return RelayDifficultyStage.Difficult;
            if (move <= 25) return RelayDifficultyStage.Advanced;
            return RelayDifficultyStage.Endless;
        }

        public float MinimumWindowAtScore(int score)
        {
            float seconds;
            switch (StageAtScore(score))
            {
                case RelayDifficultyStage.Tutorial: seconds = tutorialWindow; break;
                case RelayDifficultyStage.Beginner: seconds = beginnerWindow; break;
                case RelayDifficultyStage.Normal: seconds = normalWindow; break;
                case RelayDifficultyStage.Difficult: seconds = difficultWindow; break;
                case RelayDifficultyStage.Advanced: seconds = advancedWindow; break;
                default: seconds = Mathf.Max(advancedWindow, endlessWindow); break;
            }
            return Mathf.Max(0.01f, seconds);
        }

        private float CaptureAssistAtScore(int score)
        {
            switch (StageAtScore(score))
            {
                case RelayDifficultyStage.Tutorial: return tutorialCaptureAssist;
                case RelayDifficultyStage.Beginner: return beginnerCaptureAssist;
                case RelayDifficultyStage.Normal: return normalCaptureAssist;
                default: return 0f;
            }
        }

        public void GetGenerationLimits(int score, out float minimumGap, out float maximumGap, out float maximumStep)
        {
            if (!progressiveDifficulty)
            {
                minimumGap = HardMinimumGap;
                maximumGap = HardMaximumGap;
                maximumStep = AbsoluteHorizontalLimit * 2f;
                return;
            }
            float progress = DifficultyProgressAt(score);
            float earlyMin = Mathf.Clamp(earlyMinVerticalGap, HardMinimumGap, HardMaximumGap);
            float earlyMax = Mathf.Clamp(earlyMaxVerticalGap, earlyMin, HardMaximumGap);
            float lateMin = Mathf.Clamp(lateMinVerticalGap, earlyMin, HardMaximumGap);
            float earlyStep = Mathf.Clamp(earlyHorizontalStep, 0f, AbsoluteHorizontalLimit * 2f);
            float lateStep = Mathf.Clamp(lateHorizontalStep, earlyStep, AbsoluteHorizontalLimit * 2f);
            minimumGap = Mathf.Lerp(earlyMin, lateMin, progress);
            maximumGap = Mathf.Lerp(earlyMax, HardMaximumGap, progress);
            maximumStep = Mathf.Lerp(earlyStep, lateStep, progress);
        }

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
                while (pool.Count < Mathf.Clamp(poolSize, 12, 16))
                {
                    RelayPlanet copy = Instantiate(planetTemplates[pool.Count % planetTemplates.Length],
                        planetTemplates[0].transform.parent);
                    copy.name = "Pooled Relay Planet " + pool.Count;
                    pool.Add(copy);
                }
            }
            random = new System.Random(seed);
            foreach (RelayPlanet planet in pool)
            {
                planet.gameObject.SetActive(false);
                planet.SetLandingWindow(0f, 0f);
            }
            active.Clear();
            upcoming.Clear();
            current = pool[0];
            current.transform.position = startPosition;
            current.gameObject.SetActive(true);
            active.Add(current);
            next = null;
            playCamera.transform.position = FramedCameraPosition();
            return FillHorizon();
        }

        public float DepartureDirection(RelayPlanet landing, float arrivalAngle, float incomingDirection)
        {
            int index = upcoming.IndexOf(landing);
            if (!preferQuickDeparture || index < 0 || index + 1 >= upcoming.Count)
                return incomingDirection;
            return player.FastestDepartureDirection(landing, upcoming[index + 1],
                BoundsAt(CameraPositionFor(landing)), arrivalAngle, incomingDirection);
        }

        private Vector2 IncomingStep(RelayPlanet source)
        {
            int index = active.IndexOf(source);
            return index > 0 ? source.Center - active[index - 1].Center : Vector2.up;
        }

        public bool Advance()
        {
            if (upcoming.Count == 0)
                return false;
            current = upcoming[0];
            upcoming.RemoveAt(0);
            next = upcoming.Count > 0 ? upcoming[0] : null;
            RetirePastPlanets();
            // Recheck the actual arrival phase without moving a planet that was already visible.
            if (next != null && !player.TryFindLaunchWait(current, next.Center, next.Radius, FramedBounds,
                out _, next.MinimumLaunchWindow, next.CaptureAssist))
                return false;
            return FillHorizon();
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

        private Vector3 FramedCameraPosition() => CameraPositionFor(current);

        private Vector3 CameraPositionFor(RelayPlanet source) => new Vector3(0f,
            source != null ? source.Center.y + cameraLookAhead : startPosition.y + cameraLookAhead,
            playCamera.transform.position.z);

        private Rect BoundsAt(Vector3 point)
        {
            return RelayScreenLayout.WorldBounds(playCamera, point);
        }

        private void RetirePastPlanets()
        {
            float bottom = BoundsAt(playCamera.transform.position).yMin - retireMargin;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RelayPlanet planet = active[i];
                if (planet != current && !upcoming.Contains(planet) &&
                    planet.Center.y + player.CaptureRadius(planet) < bottom)
                {
                    planet.gameObject.SetActive(false);
                    active.RemoveAt(i);
                }
            }
        }

        private bool FillHorizon()
        {
            while (upcoming.Count < ConfiguredAheadCount)
            {
                RelayPlanet source = upcoming.Count == 0 ? current : upcoming[upcoming.Count - 1];
                int futureScore = player.Score + upcoming.Count;
                if (!PrepareNext(source, futureScore))
                {
                    // A failed distant preview must not kill a run with a playable next leg.
                    // Replenishment is attempted again on the next landing, never every frame.
                    next = upcoming.Count > 0 ? upcoming[0] : null;
                    return next != null;
                }
            }
            next = upcoming[0];
            return true;
        }

        private bool PrepareNext(RelayPlanet source, int futureScore)
        {
            // Difficulty belongs to the score when this future leg will be played, not the generation frame.
            GetGenerationLimits(futureScore, out generationMinGap, out generationMaxGap, out generationMaxStep);
            generationWindow = MinimumWindowAtScore(futureScore);
            generationCaptureAssist = CaptureAssistAtScore(futureScore);
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

            // The opening jump already has a visible lateral offset; later jumps vary across the map.
            Vector2 firstStep = new Vector2(Mathf.Clamp(source.Center.x + (source.Center.x >= 0f ? -1f : 1f) *
                Mathf.Clamp(1.6f, MinimumHorizontalStep(generationMaxStep), generationMaxStep),
                -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit),
                source.Center.y + (generationMinGap + generationMaxGap) * 0.5f);
            if (futureScore == 0 && Accept(firstStep, available, source))
                return true;
            for (int attempt = 0; attempt < candidateAttempts; attempt++)
            {
                float inward = source.Center.x >= 0f ? -1f : 1f;
                Vector2 incoming = IncomingStep(source);
                float continuing = Mathf.Abs(incoming.x) > 0.01f ? Mathf.Sign(incoming.x) : inward;
                if (Mathf.Abs(source.Center.x + continuing * MinimumHorizontalStep(generationMaxStep)) > AbsoluteHorizontalLimit)
                    continuing = inward;
                float direction = random.NextDouble() < directionPersistence ? continuing : -continuing;
                float deltaX = direction * Mathf.Lerp(MinimumHorizontalStep(generationMaxStep),
                    generationMaxStep, (float)random.NextDouble());
                if (StageAtScore(futureScore) == RelayDifficultyStage.Endless)
                {
                    // Vary sweep length while retaining the shared bend limit; never force a sharp crossing.
                    int pattern = futureScore % 3;
                    if (pattern == 0) deltaX = direction * Mathf.Lerp(generationMaxStep * 0.75f, generationMaxStep,
                        (float)random.NextDouble());
                    if (pattern == 2) deltaX = direction * Mathf.Lerp(MinimumHorizontalStep(generationMaxStep),
                        Mathf.Max(MinimumHorizontalStep(generationMaxStep), generationMaxStep * 0.75f),
                        (float)random.NextDouble());
                }
                float x = Mathf.Clamp(source.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                float gap = Mathf.Lerp(generationMinGap, generationMaxGap, (float)random.NextDouble());
                if (Accept(new Vector2(x, source.Center.y + gap), available, source))
                    return true;
            }
            // Wide timing goals can be incompatible with the current gap, capture cap and screen edges.
            // Search a bounded grid, then relax only the timing goal down to a visible Inspector floor.
            // Every fallback still uses the same two-direction flight and non-overlap rules.
            float requestedWindow = generationWindow;
            float floor = Mathf.Clamp(fallbackLaunchWindow, 0.01f, requestedWindow);
            for (int tier = 0; tier <= 3; tier++)
            {
                generationWindow = tier == 3 ? floor : Mathf.Max(floor, requestedWindow * (1f - tier * 0.25f));
                for (int side = 0; side < 2; side++)
                {
                    float inward = source.Center.x >= 0f ? -1f : 1f;
                    float direction = side == 0 ? inward : -inward;
                    for (int column = 0; column < 3; column++)
                    {
                        float deltaX = direction * Mathf.Lerp(MinimumHorizontalStep(generationMaxStep),
                            generationMaxStep, column * 0.5f);
                        float x = Mathf.Clamp(source.Center.x + deltaX, -AbsoluteHorizontalLimit, AbsoluteHorizontalLimit);
                        for (int row = 0; row < 3; row++)
                        {
                            float gap = Mathf.Lerp(generationMinGap, generationMaxGap, row * 0.5f);
                            if (Accept(new Vector2(x, source.Center.y + gap), available, source))
                                return true;
                        }
                    }
                }
                if (generationWindow <= floor) break;
            }
            return false;
        }

        private bool Accept(Vector2 center, RelayPlanet candidate, RelayPlanet source)
        {
            float gap = center.y - source.Center.y;
            const float tolerance = 0.0001f;
            if (gap < generationMinGap - tolerance || gap > generationMaxGap + tolerance ||
                Mathf.Abs(center.x) > AbsoluteHorizontalLimit + tolerance ||
                Mathf.Abs(center.x - source.Center.x) > generationMaxStep + tolerance ||
                Mathf.Abs(center.x - source.Center.x) < MinimumHorizontalStep(generationMaxStep) - tolerance)
                return false;
            if (Vector2.Angle(IncomingStep(source), center - source.Center) > maximumRelayTurn)
                return false;
            Rect futureBounds = BoundsAt(CameraPositionFor(source));
            float cap = Mathf.Clamp(maximumCaptureAssist, 0f, 0.8f);
            float baseAssist = Mathf.Clamp(generationCaptureAssist, 0f, cap);
            bool accepted = false;
            for (int tier = 0; tier <= 8; tier++)
            {
                float assist = Mathf.Min(cap, baseAssist + tier * 0.1f);
                candidate.SetLandingWindow(generationWindow, assist);
                bool separated = true;
                foreach (RelayPlanet planet in active)
                {
                    float distance = player.CaptureRadius(planet) + player.CaptureRadius(candidate) + separationMargin;
                    if ((center - planet.Center).sqrMagnitude < distance * distance)
                    {
                        separated = false;
                        break;
                    }
                }
                if (!separated) break;
                if (player.CanReachFromEitherOrbit(source, center, candidate.Radius, futureBounds,
                    generationWindow, assist))
                {
                    accepted = true;
                    break;
                }
                if (assist >= cap) break;
            }
            if (!accepted)
                return false;
            candidate.transform.position = center;
            candidate.gameObject.SetActive(true);
            candidate.RefreshVisuals();
            active.Add(candidate);
            upcoming.Add(candidate);
            next = upcoming[0];
            return true;
        }
    }
}
