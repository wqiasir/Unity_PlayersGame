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
    public TMP_InputField ipInput;          // ★ 新增：IP输入框
    public Button loginButton;

    void Start()
    {
        loginPanel.SetActive(true);
        gamePanel.SetActive(false);

        // ★ 加载上次保存的IP（如果有）
        string lastIP = PlayerPrefs.GetString("LastServerIP", "127.0.0.1");
        ipInput.text = lastIP;

        loginButton.onClick.AddListener(OnLoginButtonClicked);
    }

    void OnLoginButtonClicked()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
            nickname = "Player" + Random.Range(1000, 9999);

        string serverIP = ipInput.text.Trim();
        if (string.IsNullOrEmpty(serverIP))
            serverIP = "127.0.0.1";

        // ★ 保存IP供下次使用
        PlayerPrefs.SetString("LastServerIP", serverIP);
        PlayerPrefs.Save();

        // ★ 将IP传给NetworkManager
        NetworkManager.Instance.ConnectToServer(serverIP, nickname);

        loginPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
}