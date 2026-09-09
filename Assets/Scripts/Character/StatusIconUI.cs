using UnityEngine;
using UnityEngine.UI;

// Sinh icon debuff (hiện tại: Bleeding) lên world-space Canvas của nhân vật.
//
// Icon được gắn thẳng vào CANVAS (không phải thanh máu) nên KHÔNG bị scale/layout
// của thanh máu kéo lệch hay bóp méo. Vị trí icon do _anchoredPosition quyết định
// (đơn vị theo không gian Canvas, tính từ _anchor) — chỉnh trong Inspector để đặt
// icon đúng chỗ mong muốn quanh nhân vật.
[DisallowMultipleComponent]
public class StatusIconUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _owner;
    [SerializeField] private Sprite _bleedingIcon;

    [Header("Canvas gắn icon")]
    [Tooltip("Để trống = tự tìm Canvas cha gần nhất của object này.")]
    [SerializeField] private RectTransform _iconParent;

    [Header("Vị trí & kích thước (không gian Canvas)")]
    [Tooltip("Anchor + pivot của icon trên Canvas. (0.5,0.5) = tính từ tâm Canvas.")]
    [SerializeField] private Vector2 _anchor = new Vector2(0.5f, 0.5f);
    [Tooltip("Dịch icon so với _anchor, theo đơn vị Canvas (KHÔNG phụ thuộc thanh máu).")]
    [SerializeField] private Vector2 _anchoredPosition = new Vector2(-42f, 60f);
    [SerializeField] private float _iconSize = 26f;

    private StatusManager _statuses;
    private Image _image;

    private void Awake()
    {
        if (_owner == null || _bleedingIcon == null) return;
        _statuses = StatusManager.GetOrCreate(_owner);

        RectTransform parent = _iconParent != null ? _iconParent : ResolveCanvasParent();
        if (parent == null)
        {
            Debug.LogWarning($"[{nameof(StatusIconUI)}] {name}: không tìm thấy Canvas để gắn icon.", this);
            return;
        }

        var icon = new GameObject("BleedingStatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.layer = gameObject.layer;

        var rect = (RectTransform)icon.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = _anchor;
        rect.pivot = _anchor;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = _anchoredPosition;
        rect.sizeDelta = new Vector2(_iconSize, _iconSize);

        _image = icon.GetComponent<Image>();
        _image.sprite = _bleedingIcon;
        _image.preserveAspect = true;
        _image.raycastTarget = false;
        Refresh();
    }

    private RectTransform ResolveCanvasParent()
    {
        var canvas = GetComponentInParent<Canvas>();
        return canvas != null ? (RectTransform)canvas.transform : null;
    }

    private void OnEnable()
    {
        if (_statuses == null) return;
        _statuses.OnStatusesChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_statuses != null) _statuses.OnStatusesChanged -= Refresh;
        if (_image != null) _image.enabled = false;
    }

    private void Refresh()
    {
        if (_image != null && _statuses != null)
            _image.enabled = _statuses.HasStatus(StatusType.Bleeding);
    }
}
