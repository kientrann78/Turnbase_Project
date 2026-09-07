// Gắn component này lên MỖI Button skill (cùng cấp với Button, KHÔNG phải
// trên child Image) khi bạn muốn giữ layout "icon nằm trong khung cố định"
// (khung = targetGraphic của Button, icon = child Image riêng) nhưng vẫn
// muốn icon có phản hồi khi bấm — vì Button.Transition (Color Tint/Animation)
// mặc định chỉ tác động lên targetGraphic, không tác động lên child Image.
//
// Requires: UnityEngine.UI, đặt cùng GameObject với Button

using UnityEngine;
using UnityEngine.EventSystems;

public class SkillButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform _icon;      // child Image (icon skill) cần hiệu ứng
    [SerializeField] private float _pressedScale = 0.9f;
    [SerializeField] private float _normalScale = 1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_icon == null) return;
        _icon.localScale = Vector3.one * _pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_icon == null) return;
        _icon.localScale = Vector3.one * _normalScale;
    }
}