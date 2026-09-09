using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { Bleeding }

// Runtime status state shared by enemies and player characters.
[DisallowMultipleComponent]
public class StatusManager : MonoBehaviour
{
    private readonly Dictionary<StatusType, int> _statuses = new Dictionary<StatusType, int>();
    public event Action OnStatusesChanged;
    public int Shield { get; private set; }

    public void AddShield(int amount)
    {
        if (amount <= 0 || !isActiveAndEnabled) return;
        Shield = (int)Math.Min(int.MaxValue, (long)Shield + amount);
        OnStatusesChanged?.Invoke();
    }

    // Returns the damage left after shield absorption.
    public int AbsorbDamage(int damage)
    {
        if (damage <= 0) return 0;
        int absorbed = Math.Min(Shield, damage);
        if (absorbed > 0)
        {
            Shield -= absorbed;
            OnStatusesChanged?.Invoke();
        }
        return damage - absorbed;
    }

    public static StatusManager GetOrCreate(GameObject owner)
    {
        if (!owner.TryGetComponent(out StatusManager statuses))
            statuses = owner.AddComponent<StatusManager>();
        return statuses;
    }

    public bool HasStatus(StatusType type) => _statuses.ContainsKey(type);

    public void ApplyStatus(StatusType type, int strength)
    {
        if (strength <= 0 || !isActiveAndEnabled) return;
        _statuses[type] = strength; // Refresh, never stack.
        OnStatusesChanged?.Invoke();
    }

    public int ConsumeStatus(StatusType type)
    {
        if (!_statuses.TryGetValue(type, out int strength)) return 0;
        _statuses.Remove(type);
        OnStatusesChanged?.Invoke();
        return strength;
    }

    public void TriggerBleedingDamage(int damage, Action<int> applyDamage)
    {
        if (damage > 0 && isActiveAndEnabled)
            StartCoroutine(ApplyDelayedDamage(damage, applyDamage));
    }

    private IEnumerator ApplyDelayedDamage(int damage, Action<int> applyDamage)
    {
        yield return new WaitForSeconds(0.25f);
        applyDamage(damage); // Does not consume a newly applied status.
    }

    public void ClearAll()
    {
        StopAllCoroutines();
        _statuses.Clear();
        Shield = 0;
        OnStatusesChanged?.Invoke();
    }

    private void OnDisable() => ClearAll();
}
