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

    // Gọi từ SkillExecutor/CombatActionQueue khi unit này là target
    public virtual void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        // Né đòn: roll % Evasion trước khi trừ máu
        if (Random.Range(0f, 100f) < Evasion)
        {
            // TODO: phát event/VFX "Miss" riêng nếu cần hiển thị cho người chơi
            return;
        }

        int mitigated = Mathf.Max(amount - Armor, 1); // luôn trừ tối thiểu 1 damage dù Armor cao
        _currentHealth = Mathf.Max(_currentHealth - mitigated, 0);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

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

        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
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