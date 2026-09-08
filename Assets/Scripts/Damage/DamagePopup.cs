// Gắn lên prefab Damage Popup — 1 GameObject world-space chứa TextMeshPro,
// tự chạy animation (bay lên + fade out) rồi tự Destroy() khi xong.
// Billboard: luôn xoay mặt về camera, không bị méo theo góc nhìn.
//
// QUAN TRỌNG: script này PHẢI gắn TRỰC TIẾP lên chính GameObject có component
// Canvas (không phải lên 1 object cha rỗng chứa Canvas bên trong). Nếu tách
// riêng, Canvas dùng RectTransform — world position hiển thị thực tế của nó
// có thể lệch khỏi transform.position của object cha, khiến Instantiate(pos)
// set sai chỗ (popup luôn kẹt ở góc/giữa màn hình bất kể spawn position).
//
// Setup prefab trong Unity:
//   DamagePopup (root)
//   ├─ Canvas (component, Render Mode = World Space, Sorting Layer trên cùng)
//   ├─ DamagePopup (script này, CÙNG GameObject với Canvas)
//   └─ Text (TextMeshProUGUI, con của root — kéo vào field _text bên dưới)
//
// Requires: Canvas (cùng GameObject, Render Mode = World Space), TextMeshPro

using UnityEngine;
using TMPro;

public enum DamagePopupType
{
    Damage,
    Heal,
    CriticalDamage,
    Miss
}

[RequireComponent(typeof(Canvas))]
public class DamagePopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _text;

    [Header("Colors")]
    [SerializeField] private Color _damageColor = new Color(0.9f, 0.15f, 0.15f);
    [SerializeField] private Color _healColor = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color _criticalColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color _missColor = new Color(0.75f, 0.75f, 0.75f);

    [Header("Animation")]
    [SerializeField] private float _lifetime = 1f;
    [SerializeField] private float _riseDistance = 1.2f;
    [SerializeField] private AnimationCurve _riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.Linear(0.5f, 1f, 1f, 0f); // giữ đục nửa đầu, fade nửa sau
    [Tooltip("Crit hiển thị to hơn text thường bao nhiêu lần")]
    [SerializeField] private float _criticalScaleMultiplier = 1.4f;

    private Camera _mainCamera;
    private Vector3 _startPos;
    private Vector3 _baseScale;
    private float _elapsed;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _startPos = transform.position;
        _baseScale = transform.localScale; // giữ nguyên scale đã set trên prefab (VD 0.01)

        if (_text == null)
            Debug.LogError($"[{nameof(DamagePopup)}] {name} chưa gán _text.", this);
    }

    // Gọi ngay sau khi Instantiate để khởi tạo nội dung + màu sắc
    public void Setup(int amount, DamagePopupType type)
    {
        if (_text == null) return;

        _text.text = type switch
        {
            DamagePopupType.Heal => $"+{amount}",
            DamagePopupType.Miss => "Miss",
            _ => amount.ToString()
        };

        _text.color = type switch
        {
            DamagePopupType.Heal => _healColor,
            DamagePopupType.CriticalDamage => _criticalColor,
            DamagePopupType.Miss => _missColor,
            _ => _damageColor
        };

        transform.localScale = type == DamagePopupType.CriticalDamage
            ? _baseScale * _criticalScaleMultiplier
            : _baseScale;
    }

    private void Update()
    {
        // Billboard: luôn quay mặt về camera
        if (_mainCamera != null)
            transform.forward = _mainCamera.transform.forward;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _lifetime);

        transform.position = _startPos + Vector3.up * (_riseCurve.Evaluate(t) * _riseDistance);

        if (_text != null)
        {
            Color c = _text.color;
            c.a = _fadeCurve.Evaluate(t);
            _text.color = c;
        }

        if (_elapsed >= _lifetime)
            Destroy(gameObject);
    }
}