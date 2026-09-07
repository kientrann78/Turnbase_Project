// Base stats gốc cho 1 loại character (Knight, Mage, Rogue...).
// Đây là data asset BẤT BIẾN — không được ghi runtime vào (equipment/buff
// không mutate trực tiếp field ở đây, mà cộng modifier ở StatSheet runtime
// nằm trong CombatUnit). Nhiều character cùng class có thể share 1 asset này
// an toàn vì nó chỉ đọc, không bị thay đổi.
// Requires: none (pure data asset)

using UnityEngine;

[CreateAssetMenu(fileName = "NewCombatUnitStats", menuName = "Combat/Combat Unit Stats")]
public class CombatUnitStatsSO : ScriptableObject
{
    [Header("Info")]
    public string UnitName;

    [Header("Health & Spirit")]
    [Tooltip("Máu tối đa gốc, chưa tính buff từ trang bị")]
    public int BaseMaxHealth = 100;

    [Tooltip("Spirit (mana/energy) tối đa gốc — dùng để trả cost skill (SkillCostType.Energy)")]
    public int BaseMaxSpirit = 50;

    [Header("Offense")]
    [Tooltip("Damage cơ bản dùng cho Basic Attack")]
    public int BaseDamage = 12;

    [Tooltip("Hệ số damage riêng cho skill, cộng thêm ngoài BaseDamage nếu skill cần scale theo unit")]
    public int BaseSkillDamage = 18;

    [Header("Defense")]
    [Tooltip("Giáp — giảm damage nhận vào. Công thức áp dụng nằm ở CombatUnit.TakeDamage()")]
    public int BaseArmor = 5;

    [Tooltip("Né tránh, tính theo %. VD 5 = 5% cơ hội né đòn hoàn toàn")]
    [Range(0f, 100f)]
    public float BaseEvasion = 5f;
}