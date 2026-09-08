// Thanh chỉ số (Health/Spirit) với hiệu ứng "đuổi theo" kiểu Slay the Spire:
// - Layer chính (_fillMain) nhảy tức thì theo giá trị mới.
// - Layer đuổi (_fillChase) lerp mượt về giá trị mới sau 1 khoảng delay.
//   + Khi giảm (damage): _fillMain tụt trước, _fillChase (màu trắng/vàng) từ từ tụt theo sau,
//     tạo cảm giác "mất máu" rõ ràng.
//   + Khi tăng (heal): _fillMain tăng tức thì, _fillChase đuổi theo mượt từ dưới lên.
// Dùng Image (Type = Filled) cho cả 2 layer — ổn định, không phụ thuộc Layout Rebuild
// như Slider, và fillAmount set trực tiếp bằng code rất rẻ.
//
// Setup trong Unity:
//   StatBarRoot (RectTransform, kích thước cố định = kích thước thanh)
//   ├─ BG (Image, màu nền tối)
//   ├─ FillChase (Image, Type=Filled, Fill Method=Horizontal) — kéo lên trên BG
//   └─ FillMain  (Image, Type=Filled, Fill Method=Horizontal) — kéo lên trên FillChase
//
// Thứ tự layer TỪ DƯỚI LÊN: BG -> FillChase -> FillMain (FillMain phải nằm trên
// cùng để nhìn thấy rõ, FillChase lộ ra ở phần chênh lệch trong lúc đang đuổi).
//
// Requires: UnityEngine.UI (Image component, Type = Filled)

using UnityEngine;
using UnityEngine.UI;

public class StatBar : MonoBehaviour
{
    [Header("Fill Images (Type = Filled, Fill Method = Horizontal)")]
    [SerializeField] private Image _fillMain;   // nhảy tức thì theo giá trị mới
    [SerializeField] private Image _fillChase;  // đuổi theo mượt, tạo hiệu ứng tuột/hồi

    [Header("Chase Timing")]
    [Tooltip("Thời gian chờ trước khi FillChase bắt đầu đuổi theo FillMain (giây)")]
    [SerializeField] private float _chaseDelay = 0.3f;
    [Tooltip("Tốc độ FillChase di chuyển để bắt kịp FillMain (đơn vị fillAmount/giây)")]
    [SerializeField] private float _chaseSpeed = 0.6f;

    private float _targetFill = 1f;   // giá trị fillAmount đích (current / max)
    private float _chaseTimer;        // đếm ngược delay trước khi chase bắt đầu chạy
    private bool _isChasing;

    private void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (_fillMain == null)
            Debug.LogError($"[{nameof(StatBar)}] {name} chưa gán _fillMain.", this);

        if (_fillChase == null)
            Debug.LogError($"[{nameof(StatBar)}] {name} chưa gán _fillChase.", this);
    }

    private void Update()
    {
        if (!_isChasing || _fillChase == null)
            return;

        if (_chaseTimer > 0f)
        {
            _chaseTimer -= Time.deltaTime;
            return;
        }

        _fillChase.fillAmount = Mathf.MoveTowards(_fillChase.fillAmount, _targetFill, _chaseSpeed * Time.deltaTime);

        if (Mathf.Approximately(_fillChase.fillAmount, _targetFill))
            _isChasing = false;
    }

    // Gọi hàm này mỗi khi current/max thay đổi (OnHealthChanged, OnSpiritChanged, v.v.)
    public void SetValue(int current, int max)
    {
        if (max <= 0)
        {
            Debug.LogWarning($"[{nameof(StatBar)}] {name} nhận max <= 0, bỏ qua.", this);
            return;
        }

        _targetFill = Mathf.Clamp01((float)current / max);

        if (_fillMain != null)
            _fillMain.fillAmount = _targetFill;

        // Reset delay mỗi lần giá trị đổi — tránh chase "giật" nếu bị spam damage liên tục
        _chaseTimer = _chaseDelay;
        _isChasing = true;
    }

    // Set tức thì, không có hiệu ứng đuổi — dùng lúc khởi tạo lần đầu (OnEnable)
    // để tránh FillChase chạy hiệu ứng "tuột từ đầy về giá trị ban đầu".
    public void SetValueInstant(int current, int max)
    {
        if (max <= 0)
        {
            Debug.LogWarning($"[{nameof(StatBar)}] {name} nhận max <= 0, bỏ qua.", this);
            return;
        }

        _targetFill = Mathf.Clamp01((float)current / max);
        _isChasing = false;
        _chaseTimer = 0f;

        if (_fillMain != null)
            _fillMain.fillAmount = _targetFill;

        if (_fillChase != null)
            _fillChase.fillAmount = _targetFill;
    }
}