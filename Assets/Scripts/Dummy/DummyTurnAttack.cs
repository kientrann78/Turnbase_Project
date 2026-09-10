using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(DummyHealth))]
public sealed class DummyTurnAttack : MonoBehaviour
{
    public int IntentDamage { get; private set; }
    public bool IsAlive => isActiveAndEnabled && _health != null && !_health.IsDead;
    private DummyHealth _health;
    private GameObject _intent;
    private RectTransform _badge;
    private TextMeshProUGUI _text;
    private bool _showIntent;

    private void Awake()
    {
        _health = GetComponent<DummyHealth>();
        _intent = new GameObject("Dummy Attack Intent", typeof(RectTransform), typeof(Canvas));
        _intent.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _intent.GetComponent<Canvas>().sortingOrder = 90;
        var badge = new GameObject("Damage Badge", typeof(RectTransform), typeof(Image), typeof(Outline));
        _badge = badge.GetComponent<RectTransform>();
        _badge.SetParent(_intent.transform, false);
        _badge.anchorMin = _badge.anchorMax = Vector2.zero;
        _badge.sizeDelta = new Vector2(110f, 34f);
        badge.GetComponent<Image>().color = new Color(0.2f, 0.055f, 0.035f, 0.95f);
        badge.GetComponent<Image>().raycastTarget = false;
        badge.GetComponent<Outline>().effectColor = new Color(1f, 0.52f, 0.2f);
        _text = TurnBattleController.AddText(_badge, "Intent Damage", 19f, new Color(1f, 0.8f, 0.5f));
        SetIntentVisible(false);
    }

    public void RollIntent()
    {
        // Roll final damage once per player turn; armor must not reduce it a second time.
        IntentDamage = Random.Range(1, 11);
        RefreshIntentText();
        SetIntentVisible(true);
    }

    private void RefreshIntentText()
    {
        string label = "ATK  " + IntentDamage;
        if (_text.text != label) _text.text = label;
    }

    public void SetIntentVisible(bool visible)
    {
        _showIntent = visible;
        if (_intent != null) _intent.SetActive(visible && IsAlive);
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (!_showIntent || !IsAlive || camera == null) { if (_intent != null) _intent.SetActive(false); return; }
        RefreshIntentText();
        var sprite = GetComponentInChildren<SpriteRenderer>();
        Vector3 top = sprite != null ? new Vector3(sprite.bounds.center.x, sprite.bounds.max.y, sprite.bounds.center.z) : transform.position + Vector3.up;
        Vector3 point = camera.WorldToScreenPoint(top);
        _intent.SetActive(point.z > 0f);
        float scale = Mathf.Max(0.65f, Screen.height / 1080f);
        _badge.localScale = Vector3.one * scale;
        _badge.anchoredPosition = (Vector2)point + Vector2.up * (65f * scale);
    }

    public IEnumerator Attack(PlayerCombatUnit target)
    {
        if (!IsAlive || target == null || target.IsDead) yield break;
        int damage = IntentDamage;
        Vector3 origin = transform.position;
        Vector3 step = (target.transform.position - origin).normalized * 0.45f;
        try
        {
            float time = 0f;
            while (time < 0.3f && IsAlive)
            {
                time += Time.deltaTime;
                transform.position = origin + step * Mathf.Sin(Mathf.Clamp01(time / 0.3f) * Mathf.PI);
                yield return null;
            }
            if (IsAlive && target != null && target.isActiveAndEnabled && !target.IsDead) target.TakeDamageAfterArmor(damage);
        }
        finally { if (this != null) transform.position = origin; }
    }

    private void OnDisable() { SetIntentVisible(false); }
    private void OnDestroy() { if (_intent != null) Destroy(_intent); }
}
