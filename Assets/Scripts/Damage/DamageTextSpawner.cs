// Spawn DamagePopup mỗi khi unit này nhận damage/heal/miss.
// Dùng CHUNG cho cả Dummy (DummyHealth) và Character (CombatUnit) — tự phát
// hiện component nào có sẵn trên GameObject lúc Awake(), không cần 2 script
// riêng biệt.
//
// Setup trong Unity:
//   1. Tạo prefab DamagePopup (xem hướng dẫn trong DamagePopup.cs)
//   2. Gắn DamageTextSpawner lên Dummy HOẶC Knight/Mage/Rogue (GameObject phải
//      có sẵn DummyHealth hoặc CombatUnit — không cần cả 2)
//   3. Kéo prefab DamagePopup vào field _popupPrefab
//   4. (Tùy chọn) Kéo 1 Transform con làm _spawnPoint để chỉnh vị trí xuất
//      hiện popup (VD ngang đầu nhân vật) — nếu để trống sẽ dùng vị trí
//      GameObject này + _spawnOffset
//
// Requires: DummyHealth HOẶC CombatUnit (cùng GameObject), prefab DamagePopup

using UnityEngine;

public class DamageTextSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private DamagePopup _popupPrefab;

    [Header("Spawn Position")]
    [Tooltip("Nếu để trống, dùng transform.position + _spawnOffset")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Random lệch ngang 1 chút để nhiều popup liên tiếp không đè khít lên nhau")]
    [SerializeField] private float _horizontalJitter = 0.3f;

    private DummyHealth _dummyHealth;
    private CombatUnit _combatUnit;

    private void Awake()
    {
        // Chỉ 1 trong 2 sẽ tồn tại tùy GameObject này là Dummy hay Character
        _dummyHealth = GetComponent<DummyHealth>();
        _combatUnit = GetComponent<CombatUnit>();

        if (_dummyHealth == null && _combatUnit == null)
            Debug.LogError($"[{nameof(DamageTextSpawner)}] {name} cần DummyHealth hoặc CombatUnit trên cùng GameObject.", this);

        if (_popupPrefab == null)
            Debug.LogError($"[{nameof(DamageTextSpawner)}] {name} chưa gán _popupPrefab.", this);
    }

    private void OnEnable()
    {
        if (_dummyHealth != null)
        {
            _dummyHealth.OnDamageTaken += HandleDamageTaken;
            _dummyHealth.OnHealed += HandleHealed;
        }

        if (_combatUnit != null)
        {
            _combatUnit.OnDamageTaken += HandleDamageTaken;
            _combatUnit.OnHealed += HandleHealed;
            _combatUnit.OnAttackMissed += HandleAttackMissed;
        }
    }

    private void OnDisable()
    {
        if (_dummyHealth != null)
        {
            _dummyHealth.OnDamageTaken -= HandleDamageTaken;
            _dummyHealth.OnHealed -= HandleHealed;
        }

        if (_combatUnit != null)
        {
            _combatUnit.OnDamageTaken -= HandleDamageTaken;
            _combatUnit.OnHealed -= HandleHealed;
            _combatUnit.OnAttackMissed -= HandleAttackMissed;
        }
    }

    private void HandleDamageTaken(int actualDamage)
    {
        if (actualDamage <= 0) return; // né đòn/damage = 0 đã xử lý riêng ở HandleAttackMissed
        SpawnPopup(actualDamage, DamagePopupType.Damage);
    }

    private void HandleHealed(int actualHeal)
    {
        if (actualHeal <= 0) return;
        SpawnPopup(actualHeal, DamagePopupType.Heal);
    }

    private void HandleAttackMissed()
    {
        // Miss dùng amount = 0, DamagePopup.Setup sẽ hiện "Miss" thay vì số
        SpawnPopup(0, DamagePopupType.Miss);
    }

    private void SpawnPopup(int amount, DamagePopupType type)
    {
        if (_popupPrefab == null) return;

        Vector3 basePos = _spawnPoint != null ? _spawnPoint.position : transform.position + _spawnOffset;
        basePos.x += Random.Range(-_horizontalJitter, _horizontalJitter);

        DamagePopup popup = Instantiate(_popupPrefab, basePos, Quaternion.identity);
        popup.Setup(amount, type);
    }
}