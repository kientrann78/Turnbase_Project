using UnityEngine;

// Small, non-stacking impact shake on the active gameplay camera.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _duration = 0.12f;
    [SerializeField, Min(0f)] private float _strength = 0.04f;

    private float _remaining;
    private Vector3 _offset;

    public static void PlayHit()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        if (!mainCamera.TryGetComponent(out CameraShake shake))
            shake = mainCamera.gameObject.AddComponent<CameraShake>();

        if (shake.isActiveAndEnabled)
            shake._remaining = shake._duration;
    }

    private void Update()
    {
        RemoveOffset();
    }

    private void LateUpdate()
    {
        RemoveOffset();
        if (_remaining <= 0f || _duration <= 0f)
            return;

        _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
        float strength = _strength * (_remaining / _duration);
        // Deterministic oscillation avoids changing combat's random rolls.
        float phase = (_duration - _remaining) * 110f;
        _offset = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.3f), 0f) * strength;
        transform.localPosition += _offset;
    }

    private void RemoveOffset()
    {
        transform.localPosition -= _offset;
        _offset = Vector3.zero;
    }

    private void OnDisable()
    {
        RemoveOffset();
        _remaining = 0f;
    }
}
