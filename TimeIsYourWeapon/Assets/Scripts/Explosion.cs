using System.Collections;
using UnityEngine;

// Harmless explosion: flash, fireball, sparks, smoke, shockwave ring, light, sound and camera shake.
// It has no colliders and doesn't use physics, so it can't damage anything.
// Particles run on scaled time, so the kill slow-mo makes the explosion slow-mo too.
public class Explosion : MonoBehaviour
{
    const string MaterialPath = "VFX/ExplosionParticle"; // in a Resources folder so the shader ends up in builds
    const string SoundPath = "Audio/Explosion";
    const float VolumeBoost = 3f;
    const float Lifetime = 5f;

    static Material additiveMaterial;
    static Material alphaMaterial;
    static Material ringMaterial;
    static AudioClip sound;

    float scale;
    Light flashLight;

    // Spawns an explosion at position. scale 1 is sized for a regular enemy.
    public static void Spawn(Vector3 position, float scale = 1f)
    {
        if (!EnsureMaterials()) return;

        var go = new GameObject("Explosion");
        go.transform.position = position;
        go.AddComponent<Explosion>().Play(scale);
    }

    // Spawns an explosion sized to fit around the renderers of target.
    public static void SpawnAt(GameObject target, float sizeMultiplier = 10f)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Spawn(target.transform.position, sizeMultiplier);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // A ~2m tall enemy gives scale 1
        Spawn(bounds.center, Mathf.Max(0.5f, bounds.size.y / 2f) * sizeMultiplier);
    }

    void Play(float scale)
    {
        this.scale = scale;

        CreateFlash();
        CreateFireball();
        CreateSparks();
        CreateSmoke();
        CreateShockwave();
        CreateLight();
        PlaySound();

        CameraShake.Shake(transform.position, 0.6f * scale);
        Destroy(gameObject, Lifetime);
    }

    // ---------- Parts ----------

    void CreateFlash()
    {
        var ps = CreateSystem("Flash", additiveMaterial);
        var main = ps.main;
        main.startLifetime = 0.15f;
        main.startSpeed = 0f;
        main.startSize = 9f * scale;
        main.startColor = new Color(1f, 0.95f, 0.8f);
        Burst(ps, 1);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

        FadeOut(ps, Color.white, Color.white);
        ps.Play();
    }

    void CreateFireball()
    {
        var ps = CreateSystem("Fireball", additiveMaterial);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 11f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(2f * scale, 4.5f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f; // hot air rises
        Burst(ps, 45);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f * scale;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = 4f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.3f)));

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new[] { (new Color(1f, 1f, 0.85f), 0f), (new Color(1f, 0.75f, 0.2f), 0.15f),
                    (new Color(1f, 0.3f, 0.05f), 0.45f), (new Color(0.4f, 0.05f, 0.02f), 1f) },
            new[] { (1f, 0f), (1f, 0.4f), (0f, 1f) });

        ps.Play();
    }

    void CreateSparks()
    {
        var ps = CreateSystem("Sparks", additiveMaterial);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f * scale, 35f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * scale, 0.2f * scale);
        main.startColor = new Color(1f, 0.8f, 0.35f);
        main.gravityModifier = 1.2f;
        Burst(ps, 90);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * scale;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = 1.5f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 2f;

        FadeOut(ps, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.3f, 0.05f));
        ps.Play();
    }

    void CreateSmoke()
    {
        var ps = CreateSystem("Smoke", alphaMaterial);
        var main = ps.main;
        main.startDelay = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 5f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(2.5f * scale, 5f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.1f, 0.09f), new Color(0.28f, 0.25f, 0.23f));
        main.gravityModifier = -0.08f;
        Burst(ps, 25);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f * scale;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = 2f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new[] { (Color.white, 0f), (Color.white, 1f) },
            new[] { (0f, 0f), (0.75f, 0.15f), (0.5f, 0.6f), (0f, 1f) });

        ps.Play();
    }

    void CreateShockwave()
    {
        var ps = CreateSystem("Shockwave", ringMaterial);
        ps.transform.localPosition = Vector3.down * (0.9f * scale); // roughly at the enemy's feet
        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 22f * scale;
        main.startColor = new Color(1f, 0.7f, 0.4f);
        Burst(ps, 1);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.05f, 0f, 4f), new Keyframe(1f, 1f, 0f, 0f)));

        // Lies flat on the ground instead of facing the camera
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        FadeOut(ps, Color.white, Color.white);
        ps.Play();
    }

    void CreateLight()
    {
        var lightObject = new GameObject("Flash Light");
        lightObject.transform.SetParent(transform, false);

        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.6f, 0.25f);
        flashLight.range = 25f * scale;
        flashLight.shadows = LightShadows.None;

        StartCoroutine(FadeLight(0.8f, 40f));
    }

    void PlaySound()
    {
        if (sound == null) sound = Resources.Load<AudioClip>(SoundPath);
        if (sound == null) return;

        // Bigger explosions sound deeper and louder
        float pitch = Mathf.Clamp(Random.Range(0.9f, 1.1f) / Mathf.Sqrt(scale), 0.6f, 1.4f);
        float volume = Mathf.Clamp(0.6f + 0.4f * scale, 0.5f, 1f) * VolumeBoost;

        // AudioSource.volume caps at 1, so stack identical sources in sync to go louder
        int layers = Mathf.CeilToInt(volume);
        for (int i = 0; i < layers; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = sound;
            source.spatialBlend = 1f;
            source.minDistance = 6f * scale;
            source.maxDistance = 80f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.pitch = pitch;
            source.volume = volume / layers;
            source.Play();
        }
    }

    IEnumerator FadeLight(float duration, float intensity)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - t / duration;
            flashLight.intensity = intensity * k * k;
            yield return null;
        }

        flashLight.enabled = false;
    }

    // ---------- Helpers ----------

    ParticleSystem CreateSystem(string name, Material material)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);

        var ps = child.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // it auto-plays when added, stop to configure

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 5f; // allow the big flash / smoke when close to the camera

        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void FadeOut(ParticleSystem ps, Color from, Color to)
    {
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(new[] { (from, 0f), (to, 1f) }, new[] { (1f, 0f), (0f, 1f) });
    }

    static Gradient MakeGradient((Color color, float time)[] colors, (float alpha, float time)[] alphas)
    {
        var gradient = new Gradient();
        var colorKeys = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) colorKeys[i] = new GradientColorKey(colors[i].color, colors[i].time);
        var alphaKeys = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) alphaKeys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    // ---------- Materials & textures (created once, at runtime) ----------

    static bool EnsureMaterials()
    {
        if (additiveMaterial != null) return true;

        var baseMaterial = Resources.Load<Material>(MaterialPath);
        if (baseMaterial == null)
        {
            Debug.LogWarning($"[Explosion] Material 'Resources/{MaterialPath}' not found, no explosion.");
            return false;
        }

        // Copies, so the asset itself is never changed during Play mode
        var softDot = CreateRadialTexture(d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        var ring = CreateRadialTexture(d => Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.08f, 2f)) * Mathf.Clamp01((1f - d) * 10f));

        additiveMaterial = new Material(baseMaterial) { name = "Explosion Additive" };
        additiveMaterial.SetTexture("_BaseMap", softDot);
        additiveMaterial.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f)); // HDR so it blooms

        ringMaterial = new Material(additiveMaterial) { name = "Explosion Ring" };
        ringMaterial.SetTexture("_BaseMap", ring);

        alphaMaterial = new Material(baseMaterial) { name = "Explosion Smoke" };
        alphaMaterial.SetTexture("_BaseMap", softDot);
        alphaMaterial.SetFloat("_Blend", 0f);
        alphaMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        alphaMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        alphaMaterial.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        return true;
    }

    // d = 0 in the center, 1 at the edge
    static Texture2D CreateRadialTexture(System.Func<float, float> alpha)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            name = "Explosion Radial"
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float a = Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy)));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
