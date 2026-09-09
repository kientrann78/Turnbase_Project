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

    // Static: chỉ 1 unit được select tại 1 thời điểm trong toàn bộ đội hình.
    // Khi unit khác được click, unit đang mở phải tự đóng lại.
    private static CombatUnitUI _currentlySelected;

    private void Awake()
    {
        _unit = GetComponent<PlayerCombatUnit>();
    }

    private void OnEnable()
    {
        _unit.OnHealthChanged += HandleHealthChanged;
        _unit.OnSpiritChanged += HandleSpiritChanged;
        _unit.OnSkillSlotChanged += HandleSkillSlotChanged;
        _unit.OnDied += HandleUnitDied;

        RefreshBasicAttackIcon();
        RefreshAllSkillSlots();
        SetActionPanelVisible(false);

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
        _healthBar?.SetValueInstant(_unit.CurrentHealth, _unit.MaxHealth);
        _spiritBar?.SetValueInstant(_unit.CurrentSpirit, _unit.MaxSpirit);
    }

    private void OnDisable()
    {
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
        SelectThisUnit();
    }

    private void SelectThisUnit()
    {
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

    private void SetActionPanelVisible(bool visible)
    {
        if (!visible && !_isActing) CancelTargeting();
        if (_actionPanel != null)
            _actionPanel.SetActive(visible);
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
        if (_pendingSkill == null || _isActing) return;
        if (!_unit.CanUseSkill(_pendingSkill)) { CancelTargeting(); return; }
        if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelTargeting();
            return;
        }
        if (Time.frameCount <= _selectionFrame || Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame || IsPointerOverButton()) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        foreach (RaycastHit2D hit in Physics2D.GetRayIntersectionAll(ray))
        {
            var target = CombatTarget.FromCollider(hit.collider);
            if (target == null || !target.IsAlive || target.Transform == _unit.transform) continue;
            ConfirmTarget(target);
            break;
        }
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
        _pendingSkill = null;
        if (_selectedIcon != null) _selectedIcon.color = _originalIconColor;
        _selectedIcon = null;
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
        if (skill.Target != TargetType.SingleEnemy) return;

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
            target.TakeDamage(skill.BaseDamage);
            if (skill.BaseDamage > 0)
                target.ApplyBleeding(skill.BleedingDamage);
        }

        // 5. Về trạng thái combat bình thường
        _unit.AddShield(skill.ShieldGranted);
        _isActing = false;
        CancelTargeting();
    }
}
