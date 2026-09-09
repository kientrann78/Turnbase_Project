// Quản lý máu của Dummy: nhận sát thương, phát animation Hit, xử lý khi chết
// Requires: Animator (component gắn cùng GameObject)

using System.Collections;
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
    private int _bleedingDamage;
    private const float BleedingDamageDelay = 0.25f;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsDead => _isDead;
    public bool IsBleeding => _bleedingDamage > 0;

    public event System.Action<int, int> OnHealthChanged; // (current, max)
    public event System.Action<int> OnDamageTaken;        // (actual amount lost)
    public event System.Action<int> OnHealed;              // (actual amount restored)
    public event System.Action OnDied;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _currentHealth = _maxHealth;
    }

    // Gọi hàm này từ script gây sát thương (weapon, spell, v.v.)
    public void TakeDamage(int amount)
    {
        if (_isDead || amount <= 0)
            return;

        // Consume before damage events; this hit cannot trigger bleeding twice.
        int bleedingDamage = _bleedingDamage;
        _bleedingDamage = 0;
        ApplyDamage(amount);
        CameraShake.PlayHit();

        if (!_isDead && bleedingDamage > 0)
            StartCoroutine(ApplyBleedingDamageAfterHit(bleedingDamage));
    }

    public void ApplyBleeding(int damage)
    {
        if (_isDead || damage <= 0)
            return;

        // Refresh instead of stacking multiple applications.
        _bleedingDamage = damage;
    }

    private IEnumerator ApplyBleedingDamageAfterHit(int damage)
    {
        yield return new WaitForSeconds(BleedingDamageDelay);
        if (!_isDead)
            ApplyDamage(damage); // Separate event/popup; does not consume new bleeding.
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _bleedingDamage = 0;
    }

    private void ApplyDamage(int amount)
    {
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
        _bleedingDamage = 0;
        _animator.SetTrigger(_deathTrigger);
        OnDied?.Invoke();

        // TODO: thêm logic khi chết (disable collider, drop item, destroy sau delay, v.v.)
    }
}
