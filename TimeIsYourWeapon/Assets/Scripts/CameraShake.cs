using UnityEngine;

// Shakes the main camera's position. Uses unscaled time so the kill slow-mo doesn't drag the shake out.
// Only the position is shaken: PlayerController sets the camera rotation itself every frame.
public class CameraShake : MonoBehaviour
{
    const float MaxOffset = 0.3f;
    const float Decay = 2.5f; // trauma lost per second

    float trauma;
    Vector3 appliedOffset;

    // strength 1 = a big hit close by; weaker the further away the camera is
    public static void Shake(Vector3 source, float strength)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float distance = Vector3.Distance(cam.transform.position, source);
        float falloff = Mathf.Clamp01(1f - distance / 40f);
        if (falloff <= 0f) return;

        var shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.trauma = Mathf.Clamp01(shake.trauma + strength * falloff);
    }

    void LateUpdate()
    {
        // Undo last frame's offset first, so the camera always returns to where it belongs
        transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;

        if (trauma <= 0f || Time.timeScale == 0f) return;

        float amount = trauma * trauma;
        float t = Time.unscaledTime * 25f;
        appliedOffset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * MaxOffset * amount);
        transform.localPosition += appliedOffset;

        trauma = Mathf.Max(0f, trauma - Decay * Time.unscaledDeltaTime);
    }
}
