using UnityEngine;

public class QuitButton : MonoBehaviour
{
    // Gán hàm này vào sự kiện OnClick() của nút Quit trong Inspector
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        // Khi đang ở trong Play Mode của Editor thì thoát Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Khi build ra file thực thi thì thoát ứng dụng
        Application.Quit();
#endif
    }
}