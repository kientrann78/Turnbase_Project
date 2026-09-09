// VFX 1-shot đơn giản: phát 1 dãy Sprite lên SpriteRenderer theo tốc độ khung
// hình cho trước, rồi tự Destroy() (hoặc dừng) khi hết. Dùng cho các hiệu ứng
// đòn đánh/skill mà nghệ sĩ chỉ giao các frame rời (không kèm Animator).
//
// Setup prefab:
//   WarriorVFX1 (root)
//   ├─ SpriteRenderer (Sorting Layer/Order nằm TRÊN nhân vật + target)
//   └─ SpriteSheetAnimation (script này) — kéo các frame vào _frames
//
// Cách dùng từ code:
//   var vfx = Instantiate(prefab, worldPos, Quaternion.identity)
//                 .GetComponent<SpriteSheetAnimation>();
//   float wait = vfx.Duration;   // đọc để biết khi nào hiệu ứng kết thúc
//
// Requires: SpriteRenderer (cùng GameObject)

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetAnimation : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Sprite[] _frames;
    [Tooltip("Số frame phát mỗi giây")]
    [SerializeField] private float _framesPerSecond = 16f;
    [Tooltip("Lặp lại vô hạn thay vì chạy 1 lần")]
    [SerializeField] private bool _loop = false;

    [Header("Khi chạy xong (chỉ khi _loop = false)")]
    [SerializeField] private bool _destroyOnComplete = true;
    [Tooltip("Chờ thêm ngần này giây sau frame cuối rồi mới Destroy/tắt")]
    [SerializeField] private float _lingerAfterComplete = 0f;

    private SpriteRenderer _renderer;
    private float _elapsed;
    private bool _finished;

    // Tổng thời lượng phát hết 1 lượt các frame (giây). Caller đọc để đồng bộ
    // logic (VD: gây damage sau khi cả animation + VFX kết thúc).
    public float Duration =>
        (_frames == null || _frames.Length == 0 || _framesPerSecond <= 0f)
            ? 0f
            : _frames.Length / _framesPerSecond;

    public bool IsFinished => _finished;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();

        if (_frames == null || _frames.Length == 0)
        {
            Debug.LogWarning($"[{nameof(SpriteSheetAnimation)}] {name} chưa gán _frames.", this);
            _finished = true;
            return;
        }

        _renderer.sprite = _frames[0];
    }

    private void Update()
    {
        if (_finished || _frames == null || _frames.Length == 0)
            return;

        _elapsed += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(_elapsed * _framesPerSecond);

        if (_loop)
        {
            _renderer.sprite = _frames[frameIndex % _frames.Length];
            return;
        }

        if (frameIndex >= _frames.Length)
        {
            _renderer.sprite = _frames[_frames.Length - 1];
            Complete();
            return;
        }

        _renderer.sprite = _frames[frameIndex];
    }

    private void Complete()
    {
        _finished = true;

        if (!_destroyOnComplete)
            return;

        if (_lingerAfterComplete > 0f)
            Destroy(gameObject, _lingerAfterComplete);
        else
            Destroy(gameObject);
    }
}
