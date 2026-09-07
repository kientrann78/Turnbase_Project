using UnityEngine;
using UnityEngine.SceneManagement;
 
public class PlayButton : MonoBehaviour
{
    [Tooltip("Tên scene sẽ load khi nhấn Play (phải được thêm vào Build Settings)")]
    [SerializeField] private string gameSceneName = "Game";
 
    // Gán hàm này vào sự kiện OnClick() của nút Play trong Inspector
    public void OnPlayClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}