// UI World Space gắn theo từng character (Knight/Mage/Rogue).
// - Health/Spirit bar: LUÔN hiển thị phía trên nhân vật.
// - Action panel (Basic Attack + 3 skill button): ẩn mặc định, hiện khi người
//   chơi click vào nhân vật này, tự ẩn khi click ra ngoài hoặc chọn unit khác.
//
// Setup trong Unity (World Space Canvas):
//   Knight (GameObject)
//   ├─ PlayerCombatUnit (script đã có)
//   ├─ CombatUnitUI (script này)
//   ├─ Collider2D (để nhận click — BoxCollider2D là đủ, KHÔNG cần Rigidbody)
//   └─ Canvas (Render Mode = World Space, đặt phía trên đầu nhân vật)
//       ├─ HealthBarSlider (UI Slider)
//       ├─ SpiritBarSlider (UI Slider)
//       └─ ActionPanel (GameObject chứa 4 nút, mặc định inactive)
//           ├─ BasicAttackButton (Button)
//           │    └─ Icon (child Image — icon nằm TRONG khung nút cố định)
//           ├─ Skill1Button (Button)
//           │    └─ Icon (child Image)
//           ├─ Skill2Button (Button)
//           │    └─ Icon (child Image)
//           └─ Skill3Button (Button)
//                └─ Icon (child Image)
//
// Vì icon là child Image (không phải targetGraphic của Button), Button's
// Color Tint mặc định KHÔNG tự tạo hiệu ứng pressed trên icon. Nếu cần hiệu
// ứng khi bấm, dùng Button.Transition = Animation, hoặc gắn thêm
// SkillButtonPressEffect.cs lên mỗi Button.
//
// Click a skill to arm it, then click a living enemy to confirm the attack.
//
// Requires: PlayerCombatUnit (cùng GameObject), UnityEngine.UI

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerCombatUnit))]
public class CombatUnitUI : MonoBehaviour
{
    [Header("Bars (luôn hiện) — Image Filled + hiệu ứng đuổi theo, xem StatBar.cs")]
    [SerializeField] private StatBar _healthBar;
    [SerializeField] private StatBar _spiritBar;

    [Header("Action Panel (ẩn/hiện theo click)")]
    [SerializeField] private GameObject _actionPanel;
    [Header("Action Panel Animation")]
    [Min(0f)] [SerializeField] private float _panelOpenDuration = 0.24f;
    [SerializeField] private Vector2 _panelStartOffset = new Vector2(-45f, 0f);
    [Range(0.1f, 1f)] [SerializeField] private float _panelStartScale = 0.82f;
    [Min(0f)] [SerializeField] private float _panelCloseDuration = 0.16f;
    [Min(0f)] [SerializeField] private float _panelSkillDelay = 0.06f;
    private readonly List<PanelSkillMotion> _panelSkills = new List<PanelSkillMotion>();
    private float _panelAnimationTime;
    private bool _panelOpening;
    private bool _panelAnimating;
    private sealed class PanelSkillMotion
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector3 Position, Scale, FromPosition, FromScale;
        public float Alpha, FromAlpha;
        public bool Interactable, BlocksRaycasts;
    }

    [Header("Basic Attack (luôn có sẵn)")]
    [SerializeField] private Button _basicAttackButton;
    [Tooltip("Child Image trên _basicAttackButton — icon nằm trong khung nút cố định.")]
    [SerializeField] private Image _basicAttackIcon;

    [Header("Skill Slots ([0]=Skill Slot 1, [1]=Skill Slot 2, [2]=Skill Slot 3 — slot cố định, có thể bị thay thế skill khác lúc runtime)")]
    [SerializeField] private Button[] _skillButtons = new Button[3];
    [Tooltip("_skillIcons[i] là child Image của _skillButtons[i] (không phải targetGraphic).")]
    [SerializeField] private Image[] _skillIcons = new Image[3];

    [Tooltip("Icon hiện khi skill slot chưa unlock — để trống/placeholder, không tiết lộ skill sắp mở")]
    [SerializeField] private Sprite _lockedSkillIcon;

    private SkillDataSO _pendingSkill;
    [Header("Combat Cursors")]
    [SerializeField] private Texture2D _attackCursor;
    [SerializeField] private Texture2D _enemyCursor;
    private static Texture2D _activeCursor;
    private int _cancelCursorFrame = -1;
    private TargetPreviewFrame _targetFrame;
    private TargetPreviewFrame _buffFrame;
    private SkillAimLine _aimLine;
    private Image _selectedIcon;
    private Color _originalIconColor;
    private int _selectionFrame;
    private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

    [Header("Attack Sequence")]
    [Tooltip("Thời lượng clip animation tấn công (giây) — khớp với clip 'Attack' trong Animator. Damage chỉ áp sau khi cả animation VÀ VFX chạy xong.")]
    [SerializeField] private float _attackAnimationDuration = 0.7f;
    [Tooltip("Vị trí spawn VFX trên target. Để trống = dùng transform target + offset dưới.")]
    [SerializeField] private Vector3 _vfxOffsetOnTarget = new Vector3(0f, 1f, 0f);

    private PlayerCombatUnit _unit;

    // Chặn spam nút / chọn skill khác khi 1 đòn đánh đang diễn ra.
    private bool _isActing;
    public bool IsActing => _isActing;
    public void CloseForEnemyTurn()
    {
        CancelTargeting();
        SetActionPanelVisible(false);
        if (_currentlySelected == this) _currentlySelected = null;
    }

    // Static: chỉ 1 unit được select tại 1 thời điểm trong toàn bộ đội hình.
    // Khi unit khác được click, unit đang mở phải tự đóng lại.
    private static CombatUnitUI _currentlySelected;

    private void Awake()
    {
        _unit = GetComponent<PlayerCombatUnit>();
        CachePanelSkill(_basicAttackButton);
        foreach (Button button in _skillButtons) CachePanelSkill(button);
        _panelSkills.Sort((a, b) => b.Rect.position.y.CompareTo(a.Rect.position.y));
    }

    private void CachePanelSkill(Button button)
    {
        if (button == null || _actionPanel == null || !button.transform.IsChildOf(_actionPanel.transform)) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null || _panelSkills.Exists(item => item.Rect == rect)) return;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        _panelSkills.Add(new PanelSkillMotion {
            Rect = rect, Group = group, Position = rect.anchoredPosition3D, Scale = rect.localScale,
            Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts
        });
    }

    private void OnEnable()
    {
        _unit.OnHealthChanged += HandleHealthChanged;
        _unit.OnSpiritChanged += HandleSpiritChanged;
        _unit.OnSkillSlotChanged += HandleSkillSlotChanged;
        _unit.OnDied += HandleUnitDied;

        RefreshBasicAttackIcon();
        RefreshAllSkillSlots();
        SetActionPanelVisible(false, true);

        BindButtonClicks();
    }

    // Dùng Start() thay vì OnEnable() để đọc CurrentHealth/CurrentSpirit — Unity
    // đảm bảo TẤT CẢ Awake() (kể cả PlayerCombatUnit.Awake() gán _currentHealth
    // từ _baseStats) chạy xong trước khi BẤT KỲ Start() nào chạy, bất kể thứ tự
    // component trong Inspector hay Script Execution Order. Nếu đọc trong
    // OnEnable(), CombatUnit.Awake() có thể chưa kịp chạy -> CurrentHealth = 0
    // -> fillAmount bị set về 0 dù Base Stats đã gán đúng trong Inspector.
    private void Start()
    {
        TurnBattleController.EnsureExists();
        _healthBar?.SetValueInstant(_unit.CurrentHealth, _unit.MaxHealth);
        _spiritBar?.SetValueInstant(_unit.CurrentSpirit, _unit.MaxSpirit);
    }

    private void OnDisable()
    {
        SetActionPanelVisible(false, true);
        _unit.OnHealthChanged -= HandleHealthChanged;
        _unit.OnSpiritChanged -= HandleSpiritChanged;
        _unit.OnSkillSlotChanged -= HandleSkillSlotChanged;
        _unit.OnDied -= HandleUnitDied;
        CancelTargeting();

        UnbindButtonClicks();

        // Huỷ đòn đánh đang chạy dở nếu UI bị tắt giữa chừng — tránh damage
        // "trễ" áp sau khi object đã disabled và tránh kẹt _isActing = true.
        StopAllCoroutines();
        _isActing = false;

        if (_currentlySelected == this)
            _currentlySelected = null;
    }

    // Gọi hàm này từ Collider2D OnMouseDown, hoặc từ 1 InputManager trung tâm
    // nếu project đã có hệ thống input riêng cho việc chọn unit trong scene.
    private void OnMouseDown()
    {
        if (IsPointerOverButton()) return;
        // Let the targeting owner confirm an ally instead of switching units.
        if (_currentlySelected != null && _currentlySelected._pendingSkill != null &&
            (_currentlySelected._pendingSkill.Target == TargetType.SingleAlly ||
             _currentlySelected._pendingSkill.Target == TargetType.Self)) return;
        SelectThisUnit();
    }

    private void SelectThisUnit()
    {
        if (!TurnBattleController.CanPlayerAct) return;
        if (_unit.IsDead) return;

        // Nếu đang chọn chính unit này rồi thì click lại để đóng panel
        if (_currentlySelected == this)
        {
            SetActionPanelVisible(false);
            _currentlySelected = null;
            return;
        }

        // Đóng panel của unit đang mở trước đó (nếu có)
        if (_currentlySelected != null)
            _currentlySelected.SetActionPanelVisible(false);

        _currentlySelected = this;
        SetActionPanelVisible(true);
    }

    private void SetActionPanelVisible(bool visible, bool immediate = false)
    {
        if (!visible && !_isActing) CancelTargeting();
        if (_actionPanel == null) return;
        bool wasActive = _actionPanel.activeSelf;
        _panelOpening = visible;
        _panelAnimationTime = 0f;
        _panelAnimating = !immediate && isActiveAndEnabled && _panelSkills.Count > 0 && (visible || wasActive);
        foreach (PanelSkillMotion item in _panelSkills)
        {
            if (visible && !wasActive) SetSkillPose(item, false);
            item.FromPosition = item.Rect.anchoredPosition3D;
            item.FromScale = item.Rect.localScale;
            item.FromAlpha = item.Group.alpha;
            item.Group.interactable = false;
            item.Group.blocksRaycasts = false;
        }
        if (_panelAnimating) _actionPanel.SetActive(true);
        else
        {
            foreach (PanelSkillMotion item in _panelSkills) SetSkillPose(item, visible);
            _actionPanel.SetActive(visible);
        }
    }

    private void UpdatePanelAnimation()
    {
        if (!_panelAnimating) return;
        _panelAnimationTime += Time.unscaledDeltaTime;
        float duration = _panelOpening ? _panelOpenDuration : _panelCloseDuration;
        bool finished = true;
        for (int i = 0; i < _panelSkills.Count; i++)
        {
            PanelSkillMotion item = _panelSkills[i];
            int order = _panelOpening ? i : _panelSkills.Count - 1 - i;
            float elapsed = _panelAnimationTime - order * _panelSkillDelay;
            if (elapsed < 0f) { finished = false; continue; }
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float u = t - 1f;
            // Small overshoot on entry; accelerate inward on exit.
            float eased = _panelOpening ? 1f + 2.70158f * u * u * u + 1.70158f * u * u : t * t;
            Vector3 endPosition = item.Position + (_panelOpening ? Vector3.zero : (Vector3)_panelStartOffset);
            Vector3 endScale = item.Scale * (_panelOpening ? 1f : _panelStartScale);
            item.Rect.anchoredPosition3D = Vector3.LerpUnclamped(item.FromPosition, endPosition, eased);
            item.Rect.localScale = Vector3.LerpUnclamped(item.FromScale, endScale, eased);
            item.Group.alpha = Mathf.Lerp(item.FromAlpha, _panelOpening ? item.Alpha : 0f, t);
            if (t >= 1f) SetSkillPose(item, _panelOpening);
            else finished = false;
        }
        if (!finished) return;
        _panelAnimating = false;
        if (!_panelOpening) _actionPanel.SetActive(false);
    }

    private void SetSkillPose(PanelSkillMotion item, bool visible)
    {
        item.Rect.anchoredPosition3D = item.Position + (visible ? Vector3.zero : (Vector3)_panelStartOffset);
        item.Rect.localScale = item.Scale * (visible ? 1f : _panelStartScale);
        item.Group.alpha = visible ? item.Alpha : 0f;
        item.Group.interactable = visible && item.Interactable;
        item.Group.blocksRaycasts = visible && item.BlocksRaycasts;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (_healthBar == null) return;
        _healthBar.SetValue(current, max);
    }

    private void HandleSpiritChanged(int current, int max)
    {
        if (_spiritBar == null) return;
        _spiritBar.SetValue(current, max);
    }

    // Bắn mỗi khi 1 slot cụ thể được unlock HOẶC bị thay thế bằng skill khác —
    // chỉ refresh đúng slot đó, không cần quét lại cả 3.
    private void HandleSkillSlotChanged(int slotIndex, SkillDataSO skill)
    {
        if (!_isActing) CancelTargeting();
        RefreshSlot(slotIndex, skill);
    }

    private void RefreshBasicAttackIcon()
    {
        if (_basicAttackIcon == null) return;

        SkillDataSO basicAttack = _unit.BasicAttack;
        _basicAttackIcon.sprite = basicAttack != null ? basicAttack.Icon : null;
        _basicAttackIcon.enabled = _basicAttackIcon.sprite != null;

        if (_basicAttackButton != null)
            _basicAttackButton.interactable = basicAttack != null;
    }

    // Quét lại cả 3 slot — chỉ cần gọi 1 lần lúc OnEnable (đồng bộ trạng thái
    // ban đầu). Các thay đổi sau đó đi qua HandleSkillSlotChanged (event-driven).
    private void RefreshAllSkillSlots()
    {
        var slots = _unit.SkillSlots;
        for (int i = 0; i < _skillButtons.Length; i++)
            RefreshSlot(i, i < slots.Count ? slots[i] : null);
    }

    // Cập nhật icon + interactable cho ĐÚNG 1 slot theo index.
    // Slot trống (skill == null) hiện _lockedSkillIcon, không tiết lộ skill sắp mở.
    private void RefreshSlot(int slotIndex, SkillDataSO skill)
    {
        if (slotIndex < 0 || slotIndex >= _skillButtons.Length) return;

        bool isUnlocked = skill != null;

        if (_skillButtons[slotIndex] != null)
            _skillButtons[slotIndex].interactable = isUnlocked;

        if (_skillIcons[slotIndex] == null) return;

        Sprite iconToShow = isUnlocked ? skill.Icon : _lockedSkillIcon;
        _skillIcons[slotIndex].sprite = iconToShow;
        _skillIcons[slotIndex].enabled = iconToShow != null;
    }

    // Selection does not spend energy or start an attack.

    private void BindButtonClicks()
    {
        if (_basicAttackButton != null)
            _basicAttackButton.onClick.AddListener(HandleBasicAttackClicked);

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            if (_skillButtons[i] == null) continue;

            int slotIndex = i; // capture đúng giá trị cho closure, tránh bug "biến vòng lặp"
            _skillButtons[i].onClick.AddListener(() => HandleSkillClicked(slotIndex));
        }
    }

    private void UnbindButtonClicks()
    {
        if (_basicAttackButton != null)
            _basicAttackButton.onClick.RemoveListener(HandleBasicAttackClicked);

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            if (_skillButtons[i] == null) continue;
            _skillButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void HandleBasicAttackClicked()
    {
        SelectSkill(_unit.BasicAttack, _basicAttackIcon);
    }

    private void HandleSkillClicked(int slotIndex)
    {
        SkillDataSO skill = slotIndex < _unit.SkillSlots.Count ? _unit.SkillSlots[slotIndex] : null;
        SelectSkill(skill, slotIndex < _skillIcons.Length ? _skillIcons[slotIndex] : null);
    }

    private void SelectSkill(SkillDataSO skill, Image icon)
    {
        if (!TurnBattleController.CanPlayerAct) return;
        if (skill == null) return;

        if (_isActing)
            return; // đang có 1 đòn đánh chạy dở — bỏ qua click mới

        if (!_unit.CanUseSkill(skill))
        {
            Debug.LogWarning($"[{nameof(CombatUnitUI)}] {name} không thể dùng '{skill.SkillName}' (chưa unlock hoặc không đủ Spirit).", this);
            return;
        }

        bool deselect = _pendingSkill == skill;
        CancelTargeting();
        if (deselect) return;
        if (_currentlySelected != this) SelectThisUnit();
        _pendingSkill = skill;
        _selectionFrame = Time.frameCount;
        _selectedIcon = icon;
        if (icon != null)
        {
            _originalIconColor = icon.color;
            icon.color = new Color(icon.color.r * 0.45f, icon.color.g * 0.45f, icon.color.b * 0.45f, icon.color.a);
        }
    }

    private void LateUpdate()
    {
        UpdatePanelAnimation();
        UpdateTargeting();
        if (_currentlySelected == this) UpdateCombatCursor();
        UpdateTargetPreview();
        UpdateAimLine();
    }

    private void UpdateAimLine()
    {
        Camera camera = Camera.main;
        if (_currentlySelected != this || _pendingSkill == null || _isActing ||
            _unit.IsDead || Mouse.current == null || camera == null || !Application.isFocused)
        {
            _aimLine?.Hide();
            return;
        }
        Vector2 pointer = Mouse.current.position.ReadValue();
        if (!camera.pixelRect.Contains(pointer)) { _aimLine?.Hide(); return; }
        Collider2D body = _unit.GetComponent<Collider2D>();
        Vector3 origin = body != null && body.enabled ? body.bounds.center : _unit.transform.position;
        Vector3 screenOrigin = camera.WorldToScreenPoint(origin);
        if (screenOrigin.z <= 0f) { _aimLine?.Hide(); return; }
        if (_aimLine == null) _aimLine = new SkillAimLine();
        _aimLine.Show(screenOrigin, pointer);
    }

    private void UpdateTargeting()
    {
        if (_pendingSkill == null || _isActing) return;
        if (!_unit.CanUseSkill(_pendingSkill)) { CancelTargeting(); return; }
        if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelTargeting();
            _cancelCursorFrame = Time.frameCount;
            return;
        }
        if (Time.frameCount <= _selectionFrame || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame || IsPointerOverButton()) return;

        CombatTarget target = FindHoveredTarget(_pendingSkill);
        if (target != null) ConfirmTarget(target);
    }

    private CombatTarget FindHoveredTarget(SkillDataSO skill)
    {
        Camera camera = Camera.main;
        if (skill == null || camera == null || Mouse.current == null || IsPointerOverButton()) return null;
        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(ray))
        {
            CombatTarget target;
            if (skill.Target == TargetType.Self || skill.Target == TargetType.SingleAlly)
            {
                var ally = hit.collider.GetComponentInParent<PlayerCombatUnit>();
                if (skill.Target == TargetType.Self && ally != _unit) continue;
                target = CombatTarget.FromAlly(ally);
            }
            else if (skill.Target == TargetType.SingleEnemy)
                target = CombatTarget.FromCollider(hit.collider);
            else continue;
            if (target != null && target.IsAlive) return target;
        }
        return null;
    }

    private void UpdateTargetPreview()
    {
        _targetFrame?.Hide();
        _buffFrame?.Hide();
        if (_currentlySelected != this || _pendingSkill == null || _isActing || _unit.IsDead) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        CombatTarget target = _pendingSkill.Target == TargetType.Self
            ? CombatTarget.FromAlly(_unit) : FindHoveredTarget(_pendingSkill);
        if (target != null && target.IsAlive)
        {
            if (_targetFrame == null) _targetFrame = new TargetPreviewFrame("Target Preview");
            _targetFrame.Show(target.Transform, camera);
        }
        // Offensive skills that also grant shield apply that shield to the caster.
        if (IsAttackSkill(_pendingSkill) && _pendingSkill.ShieldGranted > 0)
        {
            if (_buffFrame == null) _buffFrame = new TargetPreviewFrame("Caster Buff Preview");
            _buffFrame.Show(_unit.transform, camera);
        }
    }

    private void OnDestroy()
    {
        _aimLine?.Dispose();
        _targetFrame?.Dispose();
        _buffFrame?.Dispose();
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        _uiHits.Clear();
        var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        EventSystem.current.RaycastAll(pointer, _uiHits);
        foreach (RaycastResult hit in _uiHits)
            if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
        return false;
    }

    private void CancelTargeting()
    {
        _aimLine?.Hide();
        _pendingSkill = null;
        _targetFrame?.Hide();
        _buffFrame?.Hide();
        if (_selectedIcon != null) _selectedIcon.color = _originalIconColor;
        _selectedIcon = null;
        if (_currentlySelected == this) SetCombatCursor(null);
    }

    private static bool IsAttackSkill(SkillDataSO skill)
    {
        return skill != null && (skill.Target == TargetType.SingleEnemy || skill.Target == TargetType.AllEnemies);
    }

    private void UpdateCombatCursor()
    {
        Texture2D cursor = null;
        if (!_isActing && !_unit.IsDead && Mouse.current != null && _cancelCursorFrame != Time.frameCount)
        {
            bool armed = IsAttackSkill(_pendingSkill);
            bool overButton = IsPointerOverButton();
            if (armed)
            {
                cursor = _attackCursor;
                if (!overButton && FindHoveredTarget(_pendingSkill) != null)
                    cursor = _enemyCursor;
            }
            else if (overButton)
            {
                foreach (RaycastResult hit in _uiHits)
                {
                    Selectable selectable = hit.gameObject.GetComponentInParent<Selectable>();
                    if (selectable == null) continue;
                    Button button = selectable as Button;
                    if (button != null && button.IsInteractable())
                    {
                        if (button == _basicAttackButton && IsAttackSkill(_unit.BasicAttack))
                            cursor = _attackCursor;
                        for (int i = 0; i < _skillButtons.Length && i < _unit.SkillSlots.Count; i++)
                            if (button == _skillButtons[i] && IsAttackSkill(_unit.SkillSlots[i]))
                                cursor = _attackCursor;
                    }
                    break;
                }
            }
        }
        SetCombatCursor(cursor);
    }

    private void SetCombatCursor(Texture2D texture)
    {
        if (_activeCursor == texture) return;
        _activeCursor = texture;
        // null restores cursor 01 configured in Player Settings.
        Vector2 hotspot = texture != null && texture == _enemyCursor
            ? new Vector2(texture.width * 0.5f, texture.height * 0.5f) : Vector2.zero;
        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
    }

    private void HandleUnitDied()
    {
        StopAllCoroutines();
        _isActing = false;
        CancelTargeting();
        SetActionPanelVisible(false);
    }

    private void ConfirmTarget(CombatTarget target)
    {
        SkillDataSO skill = _pendingSkill;
        if (skill == null || _isActing || !target.IsAlive || !_unit.CanUseSkill(skill)) return;
        if (skill.Target == TargetType.AllEnemies) return;
        bool support = skill.Target == TargetType.Self || skill.Target == TargetType.SingleAlly;
        if (support && !(target.Unit is PlayerCombatUnit)) return;
        if (skill.Target == TargetType.Self && target.Unit != _unit) return;

        if (skill.CostType == SkillCostType.Energy)
        {
            bool spent = _unit.TrySpendSpirit(skill.EnergyCost);
            if (!spent)
            {
                Debug.LogWarning($"[{nameof(CombatUnitUI)}] {name} không đủ Spirit để dùng '{skill.SkillName}' (cần {skill.EnergyCost}, hiện có {_unit.CurrentSpirit}).", this);
                return;
            }
        }

        _pendingSkill = null;
        _targetFrame?.Hide();
        _buffFrame?.Hide();
        StartCoroutine(PlayAttackThenDamage(skill, target));
    }

    // Trình tự 1 đòn đánh: animation + VFX chạy SONG SONG, damage chỉ áp sau khi
    // CẢ HAI kết thúc, rồi trả unit về trạng thái sẵn sàng.
    private IEnumerator PlayAttackThenDamage(SkillDataSO skill, CombatTarget target)
    {
        _isActing = true;

        // 1. Animation tấn công của unit này (trigger lấy từ skill, fallback "Attack")
        string trigger = string.IsNullOrEmpty(skill.AnimationTrigger) ? "Attack" : skill.AnimationTrigger;
        _unit.PlayActionAnimation(trigger);

        // 2. VFX tại vị trí target — spawn cùng lúc với animation
        float vfxDuration = 0f;
        if (skill.VfxPrefab != null && target.IsAlive)
        {
            Vector3 vfxPos = target.Transform.position + _vfxOffsetOnTarget;
            GameObject vfxGo = Instantiate(skill.VfxPrefab, vfxPos, skill.VfxPrefab.transform.rotation);

            if (vfxGo.TryGetComponent(out SpriteSheetAnimation vfxAnim))
                vfxDuration = vfxAnim.Duration;
        }

        // Finish WarriorVFX and the attack animation before spawning impact.
        float beforeImpact = Mathf.Max(_attackAnimationDuration, vfxDuration);
        bool hasImpact = skill.ImpactVfxPrefab != null && skill.BaseDamage > 0;
        float leadTime = hasImpact ? Mathf.Max(0f, skill.ImpactLeadTime) : 0f;
        if (beforeImpact > 0f)
            yield return new WaitForSeconds(beforeImpact);

        if (hasImpact && target.IsAlive)
            Instantiate(skill.ImpactVfxPrefab, target.Transform.position + skill.ImpactOffsetOnTarget,
                skill.ImpactVfxPrefab.transform.rotation);

        if (leadTime > 0f)
            yield return new WaitForSeconds(leadTime);

        // 4. Áp damage (target có thể đã chết/biến mất trong lúc chờ)
        if (target.IsAlive)
        {
            if (skill.Target == TargetType.Self || skill.Target == TargetType.SingleAlly)
            {
                target.Unit.Heal(skill.BaseHeal);
                target.Unit.AddShield(skill.ShieldGranted);
            }
            else
            {
                target.TakeDamage(skill.BaseDamage);
                if (skill.BaseDamage > 0)
                    target.ApplyBleeding(skill.BleedingDamage);
            }
        }

        // 5. Về trạng thái combat bình thường
        if (IsAttackSkill(skill)) _unit.AddShield(skill.ShieldGranted);
        _isActing = false;
        CancelTargeting();
    }
}

// Runtime UI geometry: four gold brackets, with no texture or scene setup required.
// Images never receive raycasts, so the preview cannot block target selection.
internal sealed class TargetPreviewFrame
{
    private readonly GameObject _root;
    private readonly RectTransform _rect;

    public TargetPreviewFrame(string name)
    {
        _root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var frame = new GameObject("Brackets", typeof(RectTransform));
        _rect = frame.GetComponent<RectTransform>();
        _rect.SetParent(_root.transform, false);
        _rect.anchorMin = _rect.anchorMax = Vector2.zero;
        _rect.pivot = Vector2.zero;
        for (int y = 0; y <= 1; y++)
            for (int x = 0; x <= 1; x++)
            {
                Vector2 corner = new Vector2(x, y);
                AddStroke(corner, new Vector2(21f, 5f), new Color(0.18f, 0.10f, 0.02f, 0.9f));
                AddStroke(corner, new Vector2(5f, 21f), new Color(0.18f, 0.10f, 0.02f, 0.9f));
                AddStroke(corner, new Vector2(19f, 3f), new Color(1f, 0.81f, 0.18f));
                AddStroke(corner, new Vector2(3f, 19f), new Color(1f, 0.81f, 0.18f));
            }
        Hide();
    }

    private void AddStroke(Vector2 corner, Vector2 size, Color color)
    {
        var stroke = new GameObject("Corner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = stroke.GetComponent<RectTransform>();
        rect.SetParent(_rect, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = corner;
        rect.sizeDelta = size;
        Image image = stroke.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    public void Show(Transform target, Camera camera)
    {
        // Prefer the body collider; sprite sheets can include large transparent margins.
        Collider2D body = target.GetComponent<Collider2D>();
        Bounds bounds;
        if (body != null && body.enabled) bounds = body.bounds;
        else
        {
            SpriteRenderer sprite = target.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null || !sprite.enabled) { Hide(); return; }
            bounds = sprite.bounds;
        }
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = camera.WorldToScreenPoint(new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z));
            if (point.z <= 0f) { Hide(); return; }
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float scale = Mathf.Max(0.5f, Screen.height / 1080f);
        float padding = 9f * scale;
        _rect.anchoredPosition = min - Vector2.one * padding;
        _rect.localScale = Vector3.one * scale;
        _rect.sizeDelta = (max - min + Vector2.one * (2f * padding)) / scale;
        _root.SetActive(true);
    }

    public void Hide() { if (_root != null) _root.SetActive(false); }
    public void Dispose() { if (_root != null) Object.Destroy(_root); }
}

// Reuse screen-space segments so aiming needs no materials, textures or raycasters.
internal sealed class SkillAimLine
{
    private const int SegmentCount = 28;
    private const float LineThickness = 7f;
    private const float DashSpacing = 30f;
    private const float DashFill = 0.65f;
    private readonly GameObject _root;
    private readonly RectTransform[] _segments = new RectTransform[SegmentCount];

    public SkillAimLine()
    {
        _root = new GameObject("Skill Aim Line", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        for (int i = 0; i < SegmentCount; i++)
        {
            var segment = new GameObject("Segment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.SetParent(_root.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
            Image image = segment.GetComponent<Image>();
            image.color = new Color(1f, 0.81f, 0.18f, 0.9f);
            image.raycastTarget = false;
            _segments[i] = rect;
        }
        Hide();
    }

    public void Show(Vector2 start, Vector2 pointer)
    {
        float scale = Mathf.Max(0.5f, Screen.height / 1080f);
        float distance = Vector2.Distance(start, pointer);
        if (distance < 20f * scale) { Hide(); return; }
        // Stop just short of the cursor, keeping its icon readable.
        Vector2 end = pointer - (pointer - start).normalized * (10f * scale);
        Vector2 control = (start + end) * 0.5f + Vector2.up * Mathf.Min(100f * scale, distance * 0.2f);
        int dashCount = Mathf.Clamp(Mathf.CeilToInt(distance / (DashSpacing * scale)), 1, SegmentCount);
        Vector2 previous = start;
        for (int i = 0; i < SegmentCount; i++)
        {
            RectTransform rect = _segments[i];
            rect.gameObject.SetActive(i < dashCount);
            if (i >= dashCount) continue;
            float t = (i + 1f) / dashCount;
            Vector2 point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
            Vector2 direction = point - previous;
            rect.anchoredPosition = previous;
            rect.sizeDelta = new Vector2(direction.magnitude * DashFill, LineThickness * scale);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            previous = point;
        }
        _root.SetActive(true);
    }

    public void Hide() { if (_root != null) _root.SetActive(false); }
    public void Dispose() { if (_root != null) Object.Destroy(_root); }
}
