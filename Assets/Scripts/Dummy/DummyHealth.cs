// Quản lý máu của Dummy: nhận sát thương, phát animation Hit, xử lý khi chết
// Requires: Animator (component gắn cùng GameObject)

using UnityEngine;

[RequireComponent(typeof(Animator))]
public class DummyHealth : MonoBehaviour
{
    [Header("Health Config")]
    [SerializeField] private int _maxHealth = 100;

    [Header("Animation")]
    [SerializeField] private string _hitTrigger = "Hit";   // Tên Trigger param trong Animator
    [SerializeField] private string _deathTrigger = "Death"; // Tên Trigger param khi chết (tùy chọn)

    private Animator _animator;
    private int _currentHealth;
    private bool _isDead;
    public StatusManager Statuses => StatusManager.GetOrCreate(gameObject);

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsDead => _isDead;
    public bool IsBleeding => Statuses.HasStatus(StatusType.Bleeding);
    public int Shield => Statuses.Shield;
    public void AddShield(int amount)
    {
        if (!_isDead) Statuses.AddShield(amount);
    }

    public event System.Action<int, int> OnHealthChanged; // (current, max)
    public event System.Action<int> OnDamageTaken;        // (actual amount lost)
    public event System.Action<int> OnHealed;              // (actual amount restored)
    public event System.Action OnDied;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _currentHealth = _maxHealth;
        // The former test dummy had no collider; it now needs a clickable body.
        if (GetComponentInChildren<Collider2D>() == null)
        {
            var hitbox = gameObject.AddComponent<BoxCollider2D>();
            if (TryGetComponent(out SpriteRenderer renderer) && renderer.sprite != null)
            {
                hitbox.offset = renderer.sprite.bounds.center;
                hitbox.size = renderer.sprite.bounds.size;
            }
        }
    }

    // Gọi hàm này từ script gây sát thương (weapon, spell, v.v.)
    public void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        // Consume before damage events; this hit cannot trigger bleeding twice.
        int bleedingDamage = Statuses.ConsumeStatus(StatusType.Bleeding);
        ApplyDamage(amount);
        CameraShake.PlayHit();

        if (!_isDead && bleedingDamage > 0)
            Statuses.TriggerBleedingDamage(bleedingDamage, ApplyDamage);
    }

    public void ApplyBleeding(int damage)
    {
        if (_isDead || damage <= 0)
            return;

        // Refresh instead of stacking multiple applications.
        Statuses.ApplyStatus(StatusType.Bleeding, damage);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (TryGetComponent(out StatusManager statuses)) statuses.ClearAll();
    }

    private void ApplyDamage(int amount)
    {
        if (_isDead || amount <= 0) return;
        amount = Statuses.AbsorbDamage(amount);
        if (amount <= 0) return;
        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Max(_currentHealth - amount, 0);
        int actualDamage = previousHealth - _currentHealth;

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        OnDamageTaken?.Invoke(actualDamage);

        if (_currentHealth > 0)
        {
            _animator.SetTrigger(_hitTrigger);
        }
        else
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        int actualHeal = _currentHealth - previousHealth;

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        OnHealed?.Invoke(actualHeal);
    }

    private void Die()
    {
        _isDead = true;
        Statuses.ClearAll();
        _animator.SetTrigger(_deathTrigger);
        OnDied?.Invoke();

        // TODO: thêm logic khi chết (disable collider, drop item, destroy sau delay, v.v.)
    }
}
