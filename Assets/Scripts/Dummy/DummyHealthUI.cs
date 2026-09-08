// UI World Space hiển thị thanh máu cho Dummy — đơn giản hơn CombatUnitUI vì
// Dummy không có Spirit, không có Action Panel, chỉ cần 1 StatBar theo dõi
// DummyHealth.OnHealthChanged.
//
// Setup trong Unity (World Space Canvas), giống hệt HealthBarRoot của Knight:
//   Dummy (GameObject)
//   ├─ DummyHealth (script đã có)
//   ├─ DummyHealthUI (script này)
//   └─ Canvas (Render Mode = World Space, đặt phía trên đầu Dummy)
//       └─ HealthBarRoot
//           ├─ BG        (Image, màu nền tối)
//           ├─ FillChase (Image, Type=Filled, Fill Method=Horizontal) — dưới
//           └─ FillMain  (Image, Type=Filled, Fill Method=Horizontal) — trên cùng
//
// Gắn StatBar component lên HealthBarRoot (hoặc 1 GameObject riêng chứa 2 Image
// trên), rồi kéo vào field _healthBar bên dưới.
//
// Requires: DummyHealth (cùng GameObject), StatBar (đã setup 2 layer Filled)

using UnityEngine;

[RequireComponent(typeof(DummyHealth))]
public class DummyHealthUI : MonoBehaviour
{
    [Header("Health Bar — Image Filled + hiệu ứng đuổi theo, xem StatBar.cs")]
    [SerializeField] private StatBar _healthBar;

    private DummyHealth _dummyHealth;

    private void Awake()
    {
        _dummyHealth = GetComponent<DummyHealth>();
    }

    private void OnEnable()
    {
        _dummyHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        _dummyHealth.OnHealthChanged -= HandleHealthChanged;
    }

    // Dùng Start() thay vì OnEnable() để đọc CurrentHealth — đảm bảo
    // DummyHealth.Awake() (gán _currentHealth = _maxHealth) đã chạy xong,
    // tránh lỗi fillAmount = 0 lúc khởi động do thứ tự Awake() không đảm bảo
    // giữa các script khác nhau (xem CombatUnitUI cho cùng vấn đề).
    private void Start()
    {
        _healthBar?.SetValueInstant(_dummyHealth.CurrentHealth, _dummyHealth.MaxHealth);
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (_healthBar == null) return;
        _healthBar.SetValue(current, max);
    }
}