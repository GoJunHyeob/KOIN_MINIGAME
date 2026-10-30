using UnityEngine;

namespace GravityRelay
{
    // Visual/audio feedback observes gameplay events; it never schedules launches or delays state changes.
    public sealed class RelayFeedback : MonoBehaviour
    {
        private RelayPlayer player;
        private SpriteRenderer body;
        private SpriteRenderer target;
        private Sprite baseSprite;
        private Color targetBaseColor;
        private Sprite[] expressions;
        private SpriteRenderer[] eyes;
        private Vector3[] eyeScales;
        private Quaternion[] eyeRotations;
        private LineRenderer mouth;
        private readonly LineRenderer[] glow = new LineRenderer[2];
        private readonly ParticleSystem[] bursts = new ParticleSystem[3];
        private readonly AudioSource[] voices = new AudioSource[3];
        private TrailRenderer flightTrail;
        private AudioClip[] clips;
        private Material particleMaterial;
        private int comboCap;
        private bool playing;
        private bool subscribed;
        private bool placeholder;
        private float lastLanding = -100f;

        public void Configure(RelayPlayer relayPlayer, SpriteRenderer character, SpriteRenderer marker,
            Sprite particleDot, bool isPlaceholder, AudioClip launch, AudioClip landing, AudioClip failure,
            float volume, int visualComboCap, Sprite[] expressionSprites)
        {
            Unsubscribe();
            player = relayPlayer;
            body = character;
            target = marker;
            targetBaseColor = marker.color;
            baseSprite = body.sprite;
            placeholder = isPlaceholder;
            expressions = expressionSprites;
            clips = new[] { launch, landing, failure };
            comboCap = Mathf.Max(1, visualComboCap);
            particleMaterial = new Material(body.sharedMaterial) { name = "Temporary feedback particle material" };
            particleMaterial.mainTexture = particleDot.texture;
            for (int i = 0; i < 3; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
                voices[i].dopplerLevel = 0f;
                voices[i].volume = Mathf.Clamp01(volume);
                var item = new GameObject("Pooled feedback burst " + i, typeof(ParticleSystem));
                item.transform.SetParent(transform, false);
                bursts[i] = item.GetComponent<ParticleSystem>();
                bursts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = bursts[i].main;
                main.loop = false;
                main.playOnAwake = false;
                main.maxParticles = 96;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = bursts[i].emission;
                emission.enabled = false;
                var shape = bursts[i].shape;
                shape.enabled = false;
                var renderer = item.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = particleMaterial;
                renderer.sortingOrder = 12;
                var fade = bursts[i].colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
            }
            for (int i = 0; i < glow.Length; i++)
                glow[i] = MakeLine("Combo gravity glow " + i, transform, 64, true, true, 1);
            CreateFlightTrail();
            if (placeholder)
            {
                SpriteRenderer[] children = body.GetComponentsInChildren<SpriteRenderer>(true);
                int count = Mathf.Min(2, children.Length - 1);
                eyes = new SpriteRenderer[Mathf.Max(0, count)];
                eyeScales = new Vector3[eyes.Length];
                eyeRotations = new Quaternion[eyes.Length];
                int index = 0;
                foreach (SpriteRenderer child in children)
                {
                    if (child == body || index >= eyes.Length) continue;
                    eyes[index] = child;
                    eyeScales[index] = child.transform.localScale;
                    eyeRotations[index] = child.transform.localRotation;
                    index++;
                }
                mouth = MakeLine("Temporary BCSD expression mouth", body.transform, 9, false, false, 13);
                mouth.startWidth = mouth.endWidth = 0.025f;
                mouth.startColor = mouth.endColor = new Color(0.8f, 0.64f, 1f);
            }
            Subscribe();
        }

        private void CreateFlightTrail()
        {
            var item = new GameObject("BCSD purple flight trail", typeof(TrailRenderer));
            item.transform.SetParent(body.transform, false);
            flightTrail = item.GetComponent<TrailRenderer>();
            flightTrail.time = 0.32f;
            flightTrail.minVertexDistance = 0.025f;
            flightTrail.widthMultiplier = 0.30f;
            flightTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.45f, 0.45f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.92f, 0.78f, 1f), 0f), new GradientColorKey(new Color(0.56f, 0.18f, 0.95f), 0.55f), new GradientColorKey(new Color(0.18f, 0.05f, 0.42f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.52f, 0.45f), new GradientAlphaKey(0f, 1f) });
            flightTrail.colorGradient = gradient;
            flightTrail.sharedMaterial = body.sharedMaterial;
            flightTrail.sortingOrder = Mathf.Max(0, body.sortingOrder - 1);
            flightTrail.emitting = false;
            flightTrail.autodestruct = false;
        }

        private LineRenderer MakeLine(string name, Transform parent, int points, bool loop, bool world, int order)
        {
            var item = new GameObject(name, typeof(LineRenderer));
            item.transform.SetParent(parent, false);
            LineRenderer line = item.GetComponent<LineRenderer>();
            line.sharedMaterial = body.sharedMaterial;
            line.useWorldSpace = world;
            line.positionCount = points;
            line.loop = loop;
            line.sortingOrder = order;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }

        private void Subscribe()
        {
            if (player == null || subscribed) return;
            player.Launched += OnLaunch;
            player.Landed += OnLanding;
            player.Died += OnFailure;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (player == null || !subscribed) return;
            player.Launched -= OnLaunch;
            player.Landed -= OnLanding;
            player.Died -= OnFailure;
            subscribed = false;
        }

        private void OnEnable() => Subscribe();

        public void SetPlaying(bool active) => playing = active;

        public void ResetFeedback()
        {
            lastLanding = -100f;
            playing = false;
            if (target != null) target.color = targetBaseColor;
            foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            foreach (ParticleSystem burst in bursts)
                if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (LineRenderer ring in glow) if (ring != null) ring.enabled = false;
            if (flightTrail != null)
            {
                flightTrail.emitting = false;
                flightTrail.Clear();
            }
            if (body != null) body.sprite = baseSprite;
        }

        private float Strength(int combo) => Mathf.Clamp01((combo - 1f) / Mathf.Max(1f, comboCap - 1f));

        private void PlaySound(int index, int combo)
        {
            if (clips[index] == null) return;
            voices[index].pitch = index == 2 ? 1f : Mathf.Lerp(1f, 1.35f, Strength(combo));
            voices[index].PlayOneShot(clips[index]);
        }

        private void OnLaunch(Vector2 point, int combo)
        {
            if (flightTrail != null)
            {
                flightTrail.Clear();
                flightTrail.emitting = true;
            }
            PlaySound(0, combo);
            Emit(0, point, 8 + Mathf.RoundToInt(Strength(combo) * 8f),
                new Color(0.73f, 0.6f, 1f), -player.Velocity.normalized);
        }

        private void OnLanding(Vector2 point, int combo)
        {
            lastLanding = Time.time;
            PlaySound(1, combo);
            Emit(1, point, 12 + Mathf.RoundToInt(Strength(combo) * 36f),
                new Color(0.84f, 0.65f, 1f), Vector2.zero);
        }

        private void OnFailure(Vector2 point)
        {
            if (flightTrail != null) flightTrail.emitting = false;
            PlaySound(2, 0);
            Emit(2, point, 22, new Color(1f, 0.4f, 0.66f), Vector2.zero);
        }

        private void Emit(int index, Vector2 point, int count, Color color, Vector2 direction)
        {
            ParticleSystem system = bursts[index];
            system.Play();
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var emit = new ParticleSystem.EmitParams
                {
                    position = new Vector3(point.x, point.y, 0f),
                    velocity = (Vector3)(radial * (0.8f + (i % 4) * 0.35f) + direction * 1.5f),
                    startColor = color,
                    startSize = index == 1 ? 0.06f + Strength(player.Combo) * 0.07f : 0.07f,
                    startLifetime = index == 2 ? 0.35f : 0.22f + (i % 3) * 0.06f
                };
                system.Emit(emit, 1);
            }
        }

        private void LateUpdate()
        {
            if (player == null) return;
            if (flightTrail != null)
                flightTrail.emitting = playing && (player.State == RelayState.Orbiting || player.State == RelayState.Flying);
            DrawExpression();
            bool visible = playing && player.State != RelayState.Dead && player.Combo > 0;
            foreach (LineRenderer ring in glow) ring.enabled = visible;
            if (!visible) return;
            float strength = Strength(player.Combo);
            bool orbiting = player.State == RelayState.Orbiting;
            RelayPlanet planet = orbiting ? player.CurrentPlanet : player.TargetPlanet;
            if (planet == null) return;
            float radius = orbiting ? player.OrbitRadius(planet) : player.CaptureRadius(planet);
            float impulse = Mathf.Clamp01(1f - (Time.time - lastLanding) / 0.3f);
            for (int layer = 0; layer < glow.Length; layer++)
            {
                LineRenderer ring = glow[layer];
                float alpha = (0.08f + strength * 0.13f + impulse * 0.07f) / (layer + 1f);
                ring.startColor = ring.endColor = new Color(0.73f, 0.42f, 1f, alpha);
                ring.startWidth = ring.endWidth = (0.10f + strength * 0.10f) * (layer + 1);
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, planet.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            // Keep the actual capture boundary distinct from the soft glow.
            target.color = new Color(0.8f, 0.56f, 1f, 0.85f);
        }

        private void DrawExpression()
        {
            int expression = player.State == RelayState.Dead ? 3 : player.State == RelayState.Flying ? 0
                : Time.time - lastLanding < 0.3f ? 1 : player.Combo >= 5 ? 2 : -1;
            Sprite custom = expression >= 0 && expressions != null && expression < expressions.Length
                ? expressions[expression] : null;
            body.sprite = custom != null ? custom : baseSprite;
            if (!placeholder) return;
            bool draw = custom == null;
            for (int i = 0; i < eyes.Length; i++)
            {
                eyes[i].enabled = draw;
                float height = expression == 3 ? 0.3f : expression == 0 ? 0.55f : expression == 2 ? 0.65f : 1f;
                eyes[i].transform.localScale = Vector3.Scale(eyeScales[i], new Vector3(1f, height, 1f));
                float tilt = expression == 3 ? (i == 0 ? -25f : 25f) : expression == 2 ? (i == 0 ? 15f : -15f) : 0f;
                eyes[i].transform.localRotation = eyeRotations[i] * Quaternion.Euler(0f, 0f, tilt);
            }
            mouth.enabled = draw;
            float smile = expression == 3 ? -0.05f : expression == 0 ? 0f : 0.04f + Strength(player.Combo) * 0.05f;
            for (int i = 0; i < mouth.positionCount; i++)
            {
                float t = i / (mouth.positionCount - 1f);
                mouth.SetPosition(i, new Vector3(Mathf.Lerp(-0.13f, 0.13f, t), -0.08f - Mathf.Sin(t * Mathf.PI) * smile, 0f));
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetFeedback();
            if (mouth != null) mouth.enabled = false;
            if (eyes == null) return;
            for (int i = 0; i < eyes.Length; i++)
            {
                eyes[i].transform.localScale = eyeScales[i];
                eyes[i].transform.localRotation = eyeRotations[i];
                eyes[i].enabled = true;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
