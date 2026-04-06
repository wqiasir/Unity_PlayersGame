using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject loginPanel;
    public GameObject gamePanel;

    [Header("Login UI")]
    public TMP_InputField nicknameInput;
    public Button loginButton;

    void Start()
    {
        loginPanel.SetActive(true);
        gamePanel.SetActive(false);

        loginButton.onClick.AddListener(OnLoginButtonClicked);
    }

    void OnLoginButtonClicked()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            nickname = "Player" + Random.Range(1000, 9999); // 默认昵称
        }

        // 连接到服务器
        NetworkManager.Instance.ConnectToServer(nickname);

        // 切换UI面板
        loginPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
}
