// Định nghĩa 1 skill dùng trong combat — designer tạo asset qua menu, không sửa code.
// Requires: none (pure data asset)
// Lưu ý: SO này KHÔNG giữ trạng thái runtime (VD cooldown còn lại) — trạng thái
// runtime đó nằm ở CombatUnit/PlayerCombatUnit hoặc 1 wrapper riêng (SkillInstance)
// nếu sau này cần Cooldown/Charges.

using UnityEngine;

public enum TargetType
{
    SingleEnemy,
    AllEnemies,
    Self,
    SingleAlly
}

public enum SkillCostType
{
    Cooldown,   // giới hạn theo số lượt hồi
    Energy,     // giới hạn theo điểm Spirit/Energy
    Charges     // giới hạn theo số lần dùng cố định trong trận
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "Combat/Skill Data")]
public class SkillDataSO : ScriptableObject
{
    [Header("Info")]
    public string SkillName;
    [TextArea] public string Description;
    public Sprite Icon;

    [Header("Targeting")]
    public TargetType Target = TargetType.SingleEnemy;

    [Header("Cost")]
    public SkillCostType CostType = SkillCostType.Cooldown;
    public int CooldownTurns = 2;   // dùng nếu CostType == Cooldown
    public int EnergyCost = 1;      // dùng nếu CostType == Energy
    public int MaxCharges = 1;      // dùng nếu CostType == Charges

    [Header("Effect")]
    public int BaseDamage;
    public int BaseHeal;
    [Min(0)] public int ShieldGranted;
    [Min(0)]
    [Tooltip("Bleeding applied after this attack; adds damage once on the next hit.")]
    public int BleedingDamage;
    // Mở rộng: thêm BuffDataSO[] AppliedBuffs nếu cần buff/debuff

    [Header("Presentation")]
    public GameObject VfxPrefab;
    public GameObject ImpactVfxPrefab;
    [Tooltip("World-space offset from the target for the impact effect.")]
    public Vector3 ImpactOffsetOnTarget;
    [Min(0f)] public float ImpactLeadTime = 0.5f;
    public AudioClip Sfx;
    public string AnimationTrigger; // dùng với Animator.StringToHash trong code thực thi
}
