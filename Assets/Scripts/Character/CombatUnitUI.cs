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
// [TẠM THỜI] _testTarget: gọi thẳng DummyHealth.TakeDamage() khi bấm nút,
// bỏ qua targeting system, chỉ để test nhanh. Thay bằng SkillExecutor +
// TargetingSystem thật khi hệ thống đó được implement.
//
// Requires: PlayerCombatUnit (cùng GameObject), UnityEngine.UI

using UnityEngine;
using UnityEngine.UI;

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

    [Header("[TẠM] Test Target — bỏ qua targeting system, gọi thẳng damage lên Dummy để test nhanh. Sẽ thay bằng TargetingSystem/SkillExecutor thật sau.")]
    [SerializeField] private DummyHealth _testTarget;

    private PlayerCombatUnit _unit;

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

        UnbindButtonClicks();

        if (_currentlySelected == this)
            _currentlySelected = null;
    }

    // Gọi hàm này từ Collider2D OnMouseDown, hoặc từ 1 InputManager trung tâm
    // nếu project đã có hệ thống input riêng cho việc chọn unit trong scene.
    private void OnMouseDown()
    {
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

    // ---- [TẠM] Click handling — gọi thẳng damage lên _testTarget, bỏ qua
    // targeting system. Thay bằng SkillExecutor + TargetingSystem thật khi
    // hệ thống đó được implement (xem turn-based-combat-skill mục 4.x). ----

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
        TryUseSkillOnTestTarget(_unit.BasicAttack);
    }

    private void HandleSkillClicked(int slotIndex)
    {
        SkillDataSO skill = slotIndex < _unit.SkillSlots.Count ? _unit.SkillSlots[slotIndex] : null;
        TryUseSkillOnTestTarget(skill);
    }

    private void TryUseSkillOnTestTarget(SkillDataSO skill)
    {
        if (skill == null) return;

        if (!_unit.CanUseSkill(skill))
        {
            Debug.LogWarning($"[{nameof(CombatUnitUI)}] {name} không thể dùng '{skill.SkillName}' (chưa unlock hoặc không đủ Spirit).", this);
            return;
        }

        if (skill.CostType == SkillCostType.Energy)
        {
            bool spent = _unit.TrySpendSpirit(skill.EnergyCost);
            if (!spent)
            {
                Debug.LogWarning($"[{nameof(CombatUnitUI)}] {name} không đủ Spirit để dùng '{skill.SkillName}' (cần {skill.EnergyCost}, hiện có {_unit.CurrentSpirit}).", this);
                return;
            }
        }

        if (_testTarget == null)
        {
            Debug.LogWarning($"[{nameof(CombatUnitUI)}] {name} chưa gán _testTarget, không có Dummy để nhận damage.", this);
            return;
        }

        _testTarget.TakeDamage(skill.BaseDamage);
    }
}