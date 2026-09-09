using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Canvas-space overlay avoids stretching the shield on scaled health bars.
[DisallowMultipleComponent]
public class ShieldUI : MonoBehaviour
{
    [SerializeField] private GameObject _owner;
    [SerializeField] private RectTransform _healthBarBackground;
    [Tooltip("Visible sprite bounds inside BG, normalized 0..1; excludes transparent padding.")]
    [SerializeField] private Vector4 _backgroundVisibleBounds = new Vector4(0.0625f, 0.453125f, 0.875f, 0.1875f);
    [SerializeField] private Sprite _shieldIcon;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private bool _iconOnLeft;
    [SerializeField] private Color _shieldColor = new Color(0.04f, 0.24f, 0.58f, 1f);
    [SerializeField, Min(0.01f)] private float _borderThicknessRatio = 0.12f;
    [SerializeField, Min(1f)] private float _iconHeightRatio = 1.65f;
    private StatusManager _statuses;
    private RectTransform _bar;
    private RectTransform _canvas;
    private RectTransform _root;
    private RectTransform _icon;
    private readonly RectTransform[] _edges = new RectTransform[4];
    private readonly Vector3[] _corners = new Vector3[4];
    private TextMeshProUGUI _number;

    private void Awake()
    {
        _bar = _healthBarBackground != null ? _healthBarBackground : transform.Find("BG") as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        if (_owner == null || _shieldIcon == null || _bar == null || canvas == null) return;
        _statuses = StatusManager.GetOrCreate(_owner);
        _canvas = (RectTransform)canvas.transform;
        _root = MakeRect("ShieldOverlay", _canvas);
        for (int i = 0; i < _edges.Length; i++)
        {
            _edges[i] = MakeRect("ShieldBorder" + i, _root);
            var edge = _edges[i].gameObject.AddComponent<Image>();
            edge.color = _shieldColor;
            edge.raycastTarget = false;
        }
        _icon = MakeRect("ShieldIcon", _root);
        var image = _icon.gameObject.AddComponent<Image>();
        image.sprite = _shieldIcon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var textRect = MakeRect("ShieldAmount", _icon);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-4f, -4f);
        _number = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) _number.font = _font;
        _number.alignment = TextAlignmentOptions.Center;
        _number.color = Color.white;
        _number.fontStyle = FontStyles.Bold;
        _number.enableAutoSizing = true;
        _number.fontSizeMin = 1f;
        _number.fontSizeMax = 100f;
        _number.raycastTarget = false;
        Refresh();
    }

    private RectTransform MakeRect(string objectName, RectTransform parent)
    {
        var child = new GameObject(objectName, typeof(RectTransform));
        child.layer = gameObject.layer;
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
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
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    private void Refresh()
    {
        if (_root == null) return;
        _root.gameObject.SetActive(isActiveAndEnabled && _statuses.Shield > 0);
        _number.text = _statuses.Shield.ToString();
        UpdateLayout();
    }

    private void LateUpdate()
    {
        if (_root != null && _root.gameObject.activeSelf) UpdateLayout();
    }

    private void UpdateLayout()
    {
        Rect bgRect = _bar.rect;
        // x/y = lower-left, z/w = width/height. Vector4 avoids Rect's
        // version-dependent YAML representation collapsing the overlay to zero.
        Vector4 bounds = _backgroundVisibleBounds;
        if (bounds.z <= 0f || bounds.w <= 0f)
            bounds = new Vector4(0.0625f, 0.453125f, 0.875f, 0.1875f);
        Vector2 min = bgRect.min + Vector2.Scale(bgRect.size, new Vector2(bounds.x, bounds.y));
        Vector2 max = bgRect.min + Vector2.Scale(bgRect.size, new Vector2(bounds.x + bounds.z, bounds.y + bounds.w));
        _corners[0] = _bar.TransformPoint(new Vector3(min.x, min.y, 0f));
        _corners[2] = _bar.TransformPoint(new Vector3(max.x, max.y, 0f));
        Vector3 bottomLeft = _canvas.InverseTransformPoint(_corners[0]);
        Vector3 topRight = _canvas.InverseTransformPoint(_corners[2]);
        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float height = Mathf.Abs(topRight.y - bottomLeft.y);
        float thickness = height * _borderThicknessRatio;
        // localPosition also supports canvases with non-centered pivots.
        _root.localPosition = (bottomLeft + topRight) * 0.5f;
        _root.sizeDelta = Vector2.zero;
        SetRect(_edges[0], new Vector2(0f, (height + thickness) * 0.5f), new Vector2(width + thickness * 2f, thickness));
        SetRect(_edges[1], new Vector2(0f, -(height + thickness) * 0.5f), new Vector2(width + thickness * 2f, thickness));
        SetRect(_edges[2], new Vector2(-(width + thickness) * 0.5f, 0f), new Vector2(thickness, height));
        SetRect(_edges[3], new Vector2((width + thickness) * 0.5f, 0f), new Vector2(thickness, height));
        float size = height * _iconHeightRatio;
        float side = _iconOnLeft ? -1f : 1f;
        SetRect(_icon, new Vector2(side * (width * 0.5f + size * 0.4f), 0f), new Vector2(size, size));
        _number.fontSizeMax = size * 0.5f;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    [ContextMenu("Preview/Add 10 Shield (Play Mode)")]
    private void PreviewShield()
    {
        if (!Application.isPlaying || _owner == null) return;
        if (_owner.TryGetComponent(out CombatUnit unit)) unit.AddShield(10);
        else if (_owner.TryGetComponent(out DummyHealth dummy)) dummy.AddShield(10);
    }
}
