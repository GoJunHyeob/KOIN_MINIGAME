using UnityEngine;

namespace GravityRelay
{
    public enum RelayState { Orbiting, Flying, Capturing, Dead }

    // Transform motion is authoritative; no Rigidbody2D or collision solver is needed.
    public sealed class RelayPlayer : MonoBehaviour
    {
        [SerializeField] private RelayPlanet[] planets;
        [SerializeField] private Camera playCamera;
        [SerializeField] private RelayEndlessCourse endlessCourse;
        [Header("Balance: orbit and launch")]
        [SerializeField, Min(0.05f), Tooltip("행성 표면과 캐릭터 중심 사이 여유. 가이드 링도 함께 변경됩니다.")]
        private float orbitClearance = 1f;
        [SerializeField, Min(1f), Tooltip("공전 각속도. 높을수록 발사 타이밍을 맞추기 어렵습니다.")]
        private float orbitDegreesPerSecond = 150f;
        [SerializeField, Range(10f, 20f), Tooltip("접선 방향 발사 속도 (units/s). 중력과 별도로 조정합니다.")]
        private float launchSpeed = 15f;
        [Header("Balance: gravity")]
        [SerializeField, Min(0f), Tooltip("목표 중력 세기. 높이면 궤적이 더 강하게 휘어집니다.")]
        private float gravityStrength = 60f;
        [SerializeField, Min(0.01f)] private float softening = 0.8f;
        [SerializeField, Min(0.1f)] private float maxAcceleration = 40f;
        [Header("Balance: landing and failure")]
        [SerializeField, Min(0f), Tooltip("공전 반경 바깥의 추가 포획 여유. 금색 링이 실제 포획 반경입니다.")]
        private float captureMargin = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("착륙 위치 보정 시간. 짧을수록 탁 잡히고, 길수록 부드럽게 붙습니다.")]
        private float captureDuration = 0.06f;
        [SerializeField, Min(0.1f)] private float flightTimeLimit = 4f;
        [SerializeField, Min(0f)] private float screenExitMargin = 0.25f;

        private Vector2 position;
        private Vector2 velocity;
        private Vector2 captureStart;
        private float angle;
        private float captureAngle;
        private float stateTime;
        private float orbitDirection = 1f;
        private bool launchQueued;
        private bool gameplayActive;
        private Rect flightBounds;
        public RelayState State { get; private set; }
        public int Score { get; private set; }
        public int CurrentIndex { get; private set; }
        public int TargetIndex => endlessCourse != null ? Score + 1 : (CurrentIndex + 1) % planets.Length;
        public bool CanLaunch => gameplayActive && State == RelayState.Orbiting &&
            (endlessCourse == null || endlessCourse.ReadyForLaunch);
        public RelayEndlessCourse EndlessCourse => endlessCourse;
        public float FlightRemaining => Mathf.Max(0f, flightTimeLimit - stateTime);
        public string FailureReason { get; private set; }
        public Vector2 Velocity => velocity;
        public Vector2 LaunchDirection
        {
            get
            {
                Vector2 radial = ((Vector2)transform.position - CurrentPlanet.Center).normalized;
                return new Vector2(-radial.y, radial.x) * orbitDirection;
            }
        }
        public float OrbitRadius(RelayPlanet planet) => planet.Radius + orbitClearance;
        public float CaptureRadius(RelayPlanet planet) => OrbitRadius(planet) + captureMargin + planet.CaptureAssist;
        public RelayPlanet CurrentPlanet => endlessCourse != null ? endlessCourse.CurrentPlanet : planets[CurrentIndex];
        public RelayPlanet TargetPlanet => endlessCourse != null ? endlessCourse.TargetPlanet : planets[TargetIndex];

        public void SetEndlessCourse(RelayEndlessCourse course) => endlessCourse = course;

        public void SetGameplayActive(bool active)
        {
            gameplayActive = active && State != RelayState.Dead;
            if (!gameplayActive) launchQueued = false;
        }

        public void Configure(RelayPlanet[] route, Camera camera)
        {
            planets = route;
            playCamera = camera;
        }

        private void Start()
        {
            if (planets == null || planets.Length < 2 || playCamera == null ||
                System.Array.Exists(planets, planet => planet == null))
            {
                Debug.LogError("Gravity Relay needs a camera and at least two planets.", this);
                enabled = false;
                return;
            }
            Restart();
        }

        public void Restart()
        {
            Score = 0;
            CurrentIndex = 0;
            angle = -90f * Mathf.Deg2Rad;
            orbitDirection = 1f;
            velocity = Vector2.zero;
            stateTime = 0f;
            launchQueued = false;
            FailureReason = string.Empty;
            State = RelayState.Orbiting;
            if (endlessCourse != null && !endlessCourse.ResetCourse())
            {
                Die("NO REACHABLE ROUTE");
                return;
            }
            position = OrbitPosition(CurrentPlanet, angle);
            ApplyPose();
        }

        public void RequestLaunch()
        {
            if (CanLaunch)
                launchQueued = true;
        }

        private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

        // Fixed-step gameplay simulation shared with procedural route selection.
        private void Simulate(float deltaTime)
        {
            if (!gameplayActive || State == RelayState.Dead || deltaTime <= 0f)
                return;

            if (endlessCourse != null)
                endlessCourse.Tick(deltaTime);
            stateTime += deltaTime;
            switch (State)
            {
                case RelayState.Orbiting:
                    if (launchQueued)
                    {
                        launchQueued = false;
                        Vector2 radial = (position - CurrentPlanet.Center).normalized;
                        velocity = new Vector2(-radial.y, radial.x) * (launchSpeed * orbitDirection);
                        flightBounds = CameraBounds();
                        State = RelayState.Flying;
                        stateTime = 0f;
                    }
                    else
                    {
                        angle += orbitDegreesPerSecond * Mathf.Deg2Rad * orbitDirection * deltaTime;
                        position = OrbitPosition(CurrentPlanet, angle);
                    }
                    break;
                case RelayState.Flying:
                    if (StepFlight(ref position, ref velocity, TargetPlanet.Center,
                        CaptureRadius(TargetPlanet), deltaTime))
                    {
                        BeginCapture();
                    }
                    else
                    {
                        if (stateTime >= flightTimeLimit)
                            Die("FLIGHT TIME EXPIRED");
                        else if (OutsideBounds(position, flightBounds))
                            Die("OUT OF ORBIT");
                    }
                    break;
                case RelayState.Capturing:
                    float progress = Mathf.Clamp01(stateTime / captureDuration);
                    position = Vector2.Lerp(captureStart, OrbitPosition(TargetPlanet, captureAngle),
                        Mathf.SmoothStep(0f, 1f, progress));
                    if (progress >= 1f)
                    {
                        CurrentIndex = TargetIndex;
                        angle = captureAngle;
                        Score++;
                        State = RelayState.Orbiting;
                        stateTime = 0f;
                        if (endlessCourse != null && !endlessCourse.Advance())
                            Die("NO REACHABLE ROUTE");
                    }
                    break;
            }
            ApplyPose();
        }

        private void BeginCapture()
        {
            Vector2 radial = position - TargetPlanet.Center;
            captureAngle = Mathf.Atan2(radial.y, radial.x);
            float cross = radial.x * velocity.y - radial.y * velocity.x;
            orbitDirection = cross >= 0f ? 1f : -1f;
            captureStart = position;
            velocity = Vector2.zero;
            stateTime = 0f;
            launchQueued = false;
            State = RelayState.Capturing;
        }

        private Vector2 OrbitPosition(RelayPlanet planet, float radians) =>
            planet.Center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) *
            OrbitRadius(planet);

        // Runtime flight and procedural reachability use this same integrator and swept capture check.
        private bool StepFlight(ref Vector2 point, ref Vector2 speed, Vector2 center, float radius, float dt)
        {
            Vector2 offset = center - point;
            float acceleration = Mathf.Min(maxAcceleration,
                gravityStrength / (offset.sqrMagnitude + softening * softening));
            speed += offset.normalized * (acceleration * dt);
            Vector2 step = speed * dt;
            float t = step.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector2.Dot(center - point, step) / step.sqrMagnitude) : 0f;
            Vector2 closest = point + step * t;
            bool captured = (closest - center).sqrMagnitude <= radius * radius;
            point = captured ? closest : point + step;
            return captured;
        }

        public bool TryFindLaunchWait(RelayPlanet source, Vector2 targetCenter, float targetRadius,
            Rect bounds, out int waitTicks, float minimumWindowSeconds = 0f, float captureAssist = 0f)
        {
            return FindLaunchWindow(source, targetCenter, targetRadius, bounds, angle, orbitDirection,
                minimumWindowSeconds, captureAssist, out waitTicks);
        }

        public bool CanReachFromEitherOrbit(RelayPlanet source, Vector2 targetCenter, float targetRadius, Rect bounds,
            float minimumWindowSeconds = 0f, float captureAssist = 0f)
        {
            // One extra fixed tick protects the window against an unknown future arrival phase.
            float previewWindow = Mathf.Max(minimumWindowSeconds, Time.fixedDeltaTime * 2f) + Time.fixedDeltaTime;
            return FindLaunchWindow(source, targetCenter, targetRadius, bounds, 0f, 1f, previewWindow, captureAssist, out _) &&
                FindLaunchWindow(source, targetCenter, targetRadius, bounds, 0f, -1f, previewWindow, captureAssist, out _);
        }

        private bool FindLaunchWindow(RelayPlanet source, Vector2 targetCenter, float targetRadius,
            Rect bounds, float phase, float direction, float minimumWindowSeconds, float captureAssist, out int waitTicks)
        {
            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            float orbitSeconds = 360f / Mathf.Max(0.001f, orbitDegreesPerSecond);
            int period = Mathf.Clamp(Mathf.CeilToInt(orbitSeconds / dt), 16, 360);
            float sampleSeconds = orbitSeconds / period;
            float angularStep = Mathf.PI * 2f / period * direction;
            // N successful samples span N-1 intervals; do not overstate a window by one tick.
            int requiredSamples = Mathf.Max(3, Mathf.CeilToInt(minimumWindowSeconds / sampleSeconds) + 1);
            waitTicks = 0;
            if (requiredSamples > period + 1)
                return false;
            int samples = period + requiredSamples - 1;
            int flightSteps = Mathf.CeilToInt(flightTimeLimit / dt);
            int consecutive = 0;
            int runStart = 0;
            for (int sample = 0; sample < samples; sample++)
            {
                // Wrapping joins a valid interval across 360/0 degrees instead of splitting it.
                float launchAngle = phase + (sample % period) * angularStep;
                Vector2 radial = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle));
                Vector2 point = source.Center + radial * OrbitRadius(source);
                Vector2 speed = new Vector2(-radial.y, radial.x) * (launchSpeed * direction);
                bool captured = false;
                for (int step = 0; step < flightSteps; step++)
                {
                    if (StepFlight(ref point, ref speed, targetCenter,
                        targetRadius + orbitClearance + captureMargin + captureAssist, dt))
                    {
                        captured = true;
                        break;
                    }
                    if (OutsideBounds(point, bounds))
                        break;
                }
                if (captured)
                {
                    if (consecutive == 0) runStart = sample;
                    consecutive++;
                    // This is a generation rule in game seconds, not a standalone test harness.
                    if (consecutive >= requiredSamples)
                    {
                        waitTicks = Mathf.RoundToInt((runStart % period) * sampleSeconds / dt);
                        return true;
                    }
                }
                else
                    consecutive = 0;
            }
            waitTicks = 0;
            return false;
        }

        private bool OutsideBounds(Vector2 point, Rect bounds) =>
            point.x < bounds.xMin - screenExitMargin || point.x > bounds.xMax + screenExitMargin ||
            point.y < bounds.yMin - screenExitMargin || point.y > bounds.yMax + screenExitMargin;

        private Rect CameraBounds()
        {
            Vector3 lower = playCamera.ViewportToWorldPoint(new Vector3(0f, 0f, 10f));
            Vector3 upper = playCamera.ViewportToWorldPoint(new Vector3(1f, 1f, 10f));
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        private void Die(string reason)
        {
            gameplayActive = false;
            State = RelayState.Dead;
            FailureReason = reason;
            velocity = Vector2.zero;
            launchQueued = false;
        }

        private void ApplyPose()
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            // Keep the placeholder mascot upright and readable.
        }
    }
}
