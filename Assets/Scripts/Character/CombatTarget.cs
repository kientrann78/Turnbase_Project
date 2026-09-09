using UnityEngine;

// One confirmed target for the whole attack sequence; never a preset test dummy.
public sealed class CombatTarget
{
    private readonly DummyHealth _dummy;
    private readonly CombatUnit _unit;
    private CombatTarget(DummyHealth dummy, CombatUnit unit) { _dummy = dummy; _unit = unit; }
    public Transform Transform => _dummy != null ? _dummy.transform : _unit != null ? _unit.transform : null;
    public bool IsAlive => _dummy != null
        ? _dummy.isActiveAndEnabled && !_dummy.IsDead
        : _unit != null && _unit.isActiveAndEnabled && !_unit.IsDead;

    public static CombatTarget FromCollider(Collider2D collider)
    {
        if (collider == null) return null;
        DummyHealth dummy = collider.GetComponentInParent<DummyHealth>();
        if (dummy != null) return new CombatTarget(dummy, null);
        CombatUnit unit = collider.GetComponentInParent<CombatUnit>();
        return unit != null && !(unit is PlayerCombatUnit) && unit.CompareTag("Enemy")
            ? new CombatTarget(null, unit) : null;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        if (_dummy != null) _dummy.TakeDamage(amount);
        else _unit.TakeDamage(amount);
    }

    public void ApplyBleeding(int damage)
    {
        if (!IsAlive) return;
        if (_dummy != null) _dummy.ApplyBleeding(damage);
        else _unit.ApplyBleeding(damage);
    }
}
