// Unit do người chơi điều khiển trong đội hình (Knight, Mage, Rogue...).
// Khác với enemy (sẽ dùng AI/EnemyIntentController), PlayerCombatUnit chờ
// input người chơi chọn skill + xác nhận target thông qua UI/TargetingSystem.
// Requires: CombatUnit (base), CombatUnitStatsSO đã gán trong Inspector.

using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatUnit : CombatUnit
{
    public const int SKILL_SLOT_COUNT = 3;

    [Header("Skills")]
    [Tooltip("Basic Attack — luôn có sẵn, không tốn Spirit, không tính vào 3 skill slot")]
    [SerializeField] private SkillDataSO _basicAttack;

    // Mảng CỐ ĐỊNH 3 slot (không phải list chỉ-add) — mỗi slot có thể:
    //  - null: chưa unlock
    //  - gán skill: đã unlock
    //  - bị REPLACE bằng skill khác: hỗ trợ tính năng "đổi skill" sau này
    //    (VD người chơi bỏ Skill A, gắn Skill B vào cùng slot đó).
    [SerializeField] private SkillDataSO[] _skillSlots = new SkillDataSO[SKILL_SLOT_COUNT];

    public SkillDataSO BasicAttack => _basicAttack;

    // Đọc-only view của cả 3 slot, kể cả slot null (chưa unlock) — UI dựa vào
    // index để biết chính xác slot nào đang trống/đã có skill.
    public IReadOnlyList<SkillDataSO> SkillSlots => _skillSlots;

    // Số skill đã unlock — tiện cho UI/logic không cần đếm tay
    public int UnlockedSkillCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _skillSlots.Length; i++)
                if (_skillSlots[i] != null) count++;
            return count;
        }
    }

    // Bắn khi 1 slot thay đổi nội dung (unlock lần đầu HOẶC thay thế skill khác).
    // (slotIndex, newSkill) — newSkill có thể null nếu slot bị gỡ bỏ.
    public event System.Action<int, SkillDataSO> OnSkillSlotChanged;

    protected override void Awake()
    {
        base.Awake();

        if (_basicAttack == null)
            Debug.LogWarning($"[{nameof(PlayerCombatUnit)}] {name} chưa gán Basic Attack.", this);

        if (_skillSlots == null || _skillSlots.Length != SKILL_SLOT_COUNT)
            _skillSlots = new SkillDataSO[SKILL_SLOT_COUNT];
    }

    // Gọi khi character level up / cường hóa và mở khóa skill mới vào slot TRỐNG đầu tiên.
    // Trả về false nếu đã đủ 3 skill (hard cap) hoặc skill đã có trong 1 slot khác rồi.
    public bool UnlockSkill(SkillDataSO skill)
    {
        if (skill == null) return false;

        if (HasSkill(skill))
            return false;

        int emptySlot = FindFirstEmptySlot();
        if (emptySlot < 0)
        {
            Debug.LogWarning($"[{nameof(PlayerCombatUnit)}] {name} đã đủ {SKILL_SLOT_COUNT} skill, không thể unlock thêm '{skill.SkillName}'. Dùng ReplaceSkill() nếu muốn thay thế.", this);
            return false;
        }

        SetSlot(emptySlot, skill);
        return true;
    }

    // Thay thế skill ở 1 slot cụ thể bằng skill khác — dùng cho tính năng
    // "chọn bỏ skill nào để gắn skill mới vào" sau này. Cho phép thay dù
    // slot đó đang trống hay đã có skill.
    // Trả về false nếu slotIndex không hợp lệ, skill mới null, hoặc skill
    // mới đã tồn tại ở 1 slot khác (tránh trùng skill trong cùng character).
    public bool ReplaceSkill(int slotIndex, SkillDataSO newSkill)
    {
        if (slotIndex < 0 || slotIndex >= SKILL_SLOT_COUNT) return false;
        if (newSkill == null) return false;
        if (HasSkill(newSkill)) return false;

        SetSlot(slotIndex, newSkill);
        return true;
    }

    // Gỡ skill khỏi 1 slot, trả slot đó về trạng thái trống (chưa unlock).
    public bool ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SKILL_SLOT_COUNT) return false;
        if (_skillSlots[slotIndex] == null) return false;

        SetSlot(slotIndex, null);
        return true;
    }

    public bool HasSkill(SkillDataSO skill)
    {
        if (skill == null) return false;
        for (int i = 0; i < _skillSlots.Length; i++)
            if (_skillSlots[i] == skill) return true;
        return false;
    }

    // Kiểm tra skill có dùng được không (đã unlock + đủ Spirit nếu cost là Energy).
    // SkillExecutor nên gọi hàm này trước khi cho phép người chơi xác nhận target.
    public bool CanUseSkill(SkillDataSO skill)
    {
        if (skill == null || _isDead) return false;
        if (skill != _basicAttack && !HasSkill(skill)) return false;

        if (skill.CostType == SkillCostType.Energy)
            return _currentSpirit >= skill.EnergyCost;

        // TODO: kiểm tra Cooldown/Charges khi hệ thống đó được implement (SkillInstance runtime state)
        return true;
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < _skillSlots.Length; i++)
            if (_skillSlots[i] == null) return i;
        return -1;
    }

    private void SetSlot(int slotIndex, SkillDataSO skill)
    {
        _skillSlots[slotIndex] = skill;
        OnSkillSlotChanged?.Invoke(slotIndex, skill);
    }
}