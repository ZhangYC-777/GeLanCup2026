using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// 处理角色切换、冷却和相机跟随目标。
public class SwitchPlayer : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera gameCamera;
    [SerializeField] private PlayerMove[] players = System.Array.Empty<PlayerMove>();
    [SerializeField, Min(0f)] private float switchCooldown = 0.3f;

    private PlayerInput input;
    private int currentPlayerIndex;
    private float nextSwitchTime;

    // 检查 Inspector 引用，并创建两个脚本共享的输入动作。
    private void Awake()
    {
        if (gameCamera == null || players.Length == 0 || System.Array.Exists(players, player => player == null))
        {
            Debug.LogError("请为 SwitchPlayer 指定虚拟相机，并填好角色列表。", this);
            enabled = false;
            return;
        }

        input = new PlayerInput();
    }

    // 启用输入、监听切换动作，并把控制权交给当前角色。
    private void OnEnable()
    {
        input.PlayerMaps.Switch.performed += OnSwitchPressed;
        input.PlayerMaps.Enable();
        GiveControlToCurrentPlayer();
    }

    // 停用时取消监听，并收回当前角色的移动输入。
    private void OnDisable()
    {
        if (input == null)
            return;

        input.PlayerMaps.Switch.performed -= OnSwitchPressed;
        input.PlayerMaps.Disable();

        PlayerMove player = players[currentPlayerIndex];
        if (player != null)
            player.SetMoveInput(null);
    }

    // 销毁时释放这个组件创建的输入资源。
    private void OnDestroy()
    {
        input?.Dispose();
    }

    // 冷却结束后，把控制权交给列表里的下一个角色。
    private void OnSwitchPressed(InputAction.CallbackContext context)
    {
        if (players.Length < 2 || Time.time < nextSwitchTime)
            return;

        players[currentPlayerIndex].SetMoveInput(null);
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Length;
        GiveControlToCurrentPlayer();
        nextSwitchTime = Time.time + switchCooldown;
    }

    // 交给当前角色移动输入，同时把它设为相机的跟随目标。
    private void GiveControlToCurrentPlayer()
    {
        PlayerMove player = players[currentPlayerIndex];
        player.SetMoveInput(input.PlayerMaps.Move);
        gameCamera.Follow = player.transform;
    }
}
