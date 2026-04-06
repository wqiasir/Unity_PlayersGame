using UnityEngine;
using TMPro; // 需要导入TextMeshPro

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro nicknameLabel;
    public Renderer bodyRenderer;

    private int _playerId;
    private string _nickname;
    private Vector3 _targetPosition;
    private bool _isLocalPlayer = false;
    private float _moveSpeed = 5f;

    public void Initialize(int id, string nickname, Color color)
    {
        _playerId = id;
        _nickname = nickname;
        UpdateVisuals(nickname, color);
    }

    public void UpdateVisuals(string nickname, Color color)
    {
        _nickname = nickname;
        if (nicknameLabel != null)
        {
            nicknameLabel.text = nickname;
            nicknameLabel.transform.SetParent(transform);
            nicknameLabel.transform.localPosition = new Vector3(0, 1.2f, 0);
        }
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }
    }

    public void SetAsLocalPlayer()
    {
        _isLocalPlayer = true;
        // 本地玩家可以有特殊外观，比如边框
        if (nicknameLabel != null) nicknameLabel.color = Color.yellow;
    }

    public void SetTargetPosition(Vector3 newPos)
    {
        _targetPosition = newPos;
    }

    void Update()
    {
        // 本地玩家处理鼠标输入
        if (_isLocalPlayer && Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }

        // 所有玩家（包括自己）都平滑移动到目标位置
        // 对于本地玩家，目标位置由服务器确认后更新；对于远程玩家，由广播更新
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

        // 使名字标签始终面向摄像机
        if (nicknameLabel != null)
        {
            nicknameLabel.transform.rotation = Camera.main.transform.rotation;
        }
    }

    private void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            // 假设地面有Collider，并且Layer为"Ground"
            if (hit.collider.CompareTag("Ground"))
            {
                // 发送移动请求给服务器
                NetworkManager.Instance.SendMoveRequest(hit.point);
                // 注意：本地位置不会立即更新，等待服务器确认（MOVE消息）
            }
        }
    }
}
