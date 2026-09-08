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

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsDead => _isDead;

    public event System.Action<int, int> OnHealthChanged; // (current, max)
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

        _currentHealth = Mathf.Max(_currentHealth - amount, 0);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

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

        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void Die()
    {
        _isDead = true;
        _animator.SetTrigger(_deathTrigger);
        OnDied?.Invoke();

        // TODO: thêm logic khi chết (disable collider, drop item, destroy sau delay, v.v.)
    }
}