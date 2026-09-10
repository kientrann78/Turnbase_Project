using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TurnBattleController : MonoBehaviour
{
    public static TurnBattleController Instance { get; private set; }
    public static bool CanPlayerAct => Instance == null || Instance._playerTurn && !Instance._finished;
    private bool _playerTurn;
    private bool _finished;
    private int _round;
    private Button _endTurn;
    private TextMeshProUGUI _label;
    private TextMeshProUGUI _caption;
    private PlayerCombatUnit[] _players;
    private DummyTurnAttack[] _enemies;

    public static void EnsureExists()
    {
        if (Instance == null) new GameObject("Turn Battle").AddComponent<TurnBattleController>();
    }

    private void Awake() { Instance = this; }
    private void Start()
    {
        _players = FindObjectsByType<PlayerCombatUnit>();
        var dummies = FindObjectsByType<DummyHealth>();
        System.Array.Sort(_players, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        System.Array.Sort(dummies, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        _enemies = new DummyTurnAttack[dummies.Length];
        for (int i = 0; i < dummies.Length; i++)
        {
            if (!dummies[i].TryGetComponent(out DummyTurnAttack attack))
                attack = dummies[i].gameObject.AddComponent<DummyTurnAttack>();
            _enemies[i] = attack;
        }
        BuildUI();
        BeginPlayerTurn();
    }

    private bool Busy()
    {
        foreach (var player in _players)
            if (player != null && player.TryGetComponent(out CombatUnitUI ui) && ui.IsActing) return true;
        return false;
    }

    public PlayerCombatUnit LivingPlayer()
    {
        foreach (var player in _players)
            if (player != null && player.isActiveAndEnabled && !player.IsDead) return player;
        return null;
    }

    private bool CheckFinished()
    {
        bool anyEnemy = false;
        foreach (var enemy in _enemies) if (enemy != null && enemy.IsAlive) anyEnemy = true;
        if (LivingPlayer() != null && anyEnemy) return false;
        _finished = true;
        _playerTurn = false;
        CloseActions();
        _label.text = LivingPlayer() == null ? "DEFEAT" : "VICTORY";
        _caption.text = "BATTLE COMPLETE";
        foreach (var enemy in _enemies) if (enemy != null) enemy.SetIntentVisible(false);
        return true;
    }

    private void BeginPlayerTurn()
    {
        if (CheckFinished()) return;
        _playerTurn = true;
        _round++;
        foreach (var player in _players)
            if (player != null && player.isActiveAndEnabled && !player.IsDead) player.RestoreSpirit(50);
        foreach (var enemy in _enemies) if (enemy != null && enemy.IsAlive) enemy.RollIntent();
        _label.text = "END TURN  >";
        _caption.text = "TURN " + _round + "  /  YOUR TURN";
    }

    private void Update()
    {
        if (_endTurn == null) return;
        bool busy = Busy();
        if (!_finished && !busy) CheckFinished();
        _endTurn.interactable = _playerTurn && !_finished && !busy;
    }

    private void CloseActions()
    {
        foreach (var player in _players)
            if (player != null && player.TryGetComponent(out CombatUnitUI ui)) ui.CloseForEnemyTurn();
    }

    public void EndTurn()
    {
        if (!_playerTurn || _finished || Busy()) return;
        _playerTurn = false;
        _endTurn.interactable = false;
        CloseActions();
        StartCoroutine(EnemyTurn());
    }

    private IEnumerator EnemyTurn()
    {
        _label.text = "ENEMY TURN";
        _caption.text = "TURN " + _round + "  /  DEFEND";
        foreach (var enemy in _enemies) if (enemy != null) enemy.SetIntentVisible(false);
        yield return new WaitForSeconds(0.35f);
        foreach (var enemy in _enemies)
        {
            if (_finished || LivingPlayer() == null) break;
            if (enemy == null || !enemy.IsAlive) continue;
            yield return enemy.Attack(LivingPlayer());
            yield return new WaitForSeconds(0.35f);
        }
        if (!_finished) BeginPlayerTurn();
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("Turn HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = 150;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var buttonObject = new GameObject("End Turn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-36f, 36f);
        rect.sizeDelta = new Vector2(248f, 78f);
        var background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.12f, 0.07f, 0.98f);
        var outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.65f, 0.26f);
        outline.effectDistance = new Vector2(2f, -2f);
        _endTurn = buttonObject.GetComponent<Button>();
        _endTurn.targetGraphic = background;
        var colors = _endTurn.colors;
        colors.highlightedColor = new Color(1.35f, 1.3f, 1.15f);
        colors.pressedColor = new Color(0.75f, 0.7f, 0.6f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        _endTurn.colors = colors;
        _endTurn.onClick.AddListener(EndTurn);
        _label = AddText(rect, "End Turn Label", 25f, new Color(1f, 0.86f, 0.5f));
        _caption = AddText(rect, "Turn Counter", 16f, new Color(1f, 0.92f, 0.74f));
        _caption.rectTransform.anchoredPosition = new Vector2(0f, 62f);
        _caption.rectTransform.sizeDelta = new Vector2(0f, -35f);
    }

    public static TextMeshProUGUI AddText(Transform parent, string name, float size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.sizeDelta = Vector2.zero;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
