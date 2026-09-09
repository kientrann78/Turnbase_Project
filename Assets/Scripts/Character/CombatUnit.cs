// Base class cho mọi unit tham gia combat (player-controlled hoặc enemy sau này).
// Giữ RUNTIME STATE (current Health/Spirit, IsDead) — khác với CombatUnitStatsSO
// chỉ chứa base value bất biến. Đọc base stats từ SO lúc Awake(), rồi tự quản lý
// state từ đó, không ghi ngược lại vào SO.
// Requires: Animator (component gắn cùng GameObject) — dùng để phát Hit/Death trigger

using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CombatUnit : MonoBehaviour
{
    [Header("Base Stats (SO)")]
    [SerializeField] protected CombatUnitStatsSO _baseStats;

    [Header("Animation")]
    [SerializeField] protected string _hitTrigger = "Hit";
    [SerializeField] protected string _deathTrigger = "Death";

    protected Animator _animator;

    protected int _currentHealth;
    protected int _currentSpirit;
    protected bool _isDead;

    public int CurrentHealth => _currentHealth;
    public int CurrentSpirit => _currentSpirit;
    public bool IsDead => _isDead;

    // Max hiện tại — hiện tại = base SO, sau này khi có equipment sẽ đổi thành
    // StatSheet.CurrentValue (base + modifier từ trang bị).
    public int MaxHealth => _baseStats.BaseMaxHealth;
    public int MaxSpirit => _baseStats.BaseMaxSpirit;
    public int Damage => _baseStats.BaseDamage;
    public int SkillDamage => _baseStats.BaseSkillDamage;
    public int Armor => _baseStats.BaseArmor;
    public float Evasion => _baseStats.BaseEvasion;

    public event System.Action<int, int> OnHealthChanged; // (current, max)
    public event System.Action<int, int> OnSpiritChanged;  // (current, max)
    public event System.Action<int> OnDamageTaken;         // (actual damage after armor mitigation)
    public event System.Action<int> OnHealed;               // (actual amount restored)
    public event System.Action OnAttackMissed;              // né đòn thành công (Evasion)
    public event System.Action OnDied;

    protected virtual void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_baseStats == null)
        {
            Debug.LogError($"[{nameof(CombatUnit)}] {name} chưa gán CombatUnitStatsSO!", this);
            return;
        }

        _currentHealth = _baseStats.BaseMaxHealth;
        _currentSpirit = _baseStats.BaseMaxSpirit;
    }

    // Phát animation hành động (tấn công / cast skill) của CHÍNH unit này.
    // Gọi từ UI/SkillExecutor lúc unit dùng skill, trước khi resolve damage.
    // triggerName lấy từ SkillDataSO.AnimationTrigger; rỗng thì bỏ qua.
    public virtual void PlayActionAnimation(string triggerName)
    {
        if (_isDead || _animator == null || string.IsNullOrEmpty(triggerName))
            return;

        _animator.SetTrigger(triggerName);
    }

    // Gọi từ SkillExecutor/CombatActionQueue khi unit này là target
    public virtual void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        // Né đòn: roll % Evasion trước khi trừ máu
        if (Random.Range(0f, 100f) < Evasion)
        {
            OnAttackMissed?.Invoke();
            return;
        }

        int mitigated = Mathf.Max(amount - Armor, 1); // luôn trừ tối thiểu 1 damage dù Armor cao
        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(_currentHealth - mitigated, 0);
        int actualDamage = previousHealth - _currentHealth;

        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        OnDamageTaken?.Invoke(actualDamage);
        if (actualDamage > 0)
            CameraShake.PlayHit();

        if (_currentHealth > 0)
        {
            _animator.SetTrigger(_hitTrigger);
        }
        else
        {
            Die();
        }
    }

    public virtual void Heal(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        int actualHeal = _currentHealth - previousHealth;

        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        OnHealed?.Invoke(actualHeal);
    }

    // Trả về false nếu không đủ Spirit — SkillExecutor nên kiểm tra trước khi cho phép dùng skill
    public virtual bool TrySpendSpirit(int amount)
    {
        if (amount <= 0) return true;
        if (_currentSpirit < amount) return false;

        _currentSpirit -= amount;
        OnSpiritChanged?.Invoke(_currentSpirit, MaxSpirit);
        return true;
    }

    public virtual void RestoreSpirit(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        _currentSpirit = Mathf.Min(_currentSpirit + amount, MaxSpirit);
        OnSpiritChanged?.Invoke(_currentSpirit, MaxSpirit);
    }

    protected virtual void Die()
    {
        _isDead = true;
        _animator.SetTrigger(_deathTrigger);
        OnDied?.Invoke();

        // TODO: disable collider, drop item, destroy sau delay, v.v.
    }
}
