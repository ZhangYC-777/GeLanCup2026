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
    private bool gameplayLocked;
    private bool switchingLocked;

    internal int CurrentPlayerIndex => currentPlayerIndex;

    // 存档位置与 Inspector 里的角色列表顺序对应。
    internal Vector3[] CapturePositions()
    {
        Vector3[] positions = new Vector3[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            positions[i] = players[i].SavePosition;
        }
        return positions;
    }

    // 先定位全队，再交还移动输入和相机跟随。
    internal void RestoreParty(Vector3[] positions, int activePlayerIndex)
    {
        players[currentPlayerIndex].SetMoveInput(null);
        for (int i = 0; i < Mathf.Min(players.Length, positions.Length); i++)
        {
            players[i].RestorePosition(positions[i]);
        }

        int index = activePlayerIndex >= 0 && activePlayerIndex < players.Length ? activePlayerIndex : 0;
        if (!players[index].gameObject.activeInHierarchy)
        {
            index = System.Array.FindIndex(players, player => player.gameObject.activeInHierarchy);
        }
        if (index >= 0)
        {
            SelectPlayer(players[index]);
        }
    }

    // 供交互提示读取当前操控角色。
    public Transform CurrentPlayer
    {
        get
        {
            if (!isActiveAndEnabled || input == null || players.Length == 0)
            {
                return null;
            }

            PlayerMove player = players[currentPlayerIndex];
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                return null;
            }

            return player.transform;
        }
    }

    // 为 true 时表示剧情或背包已暂停玩家移动与角色切换。
    public bool IsGameplayLocked
    {
        get { return gameplayLocked; }
    }

    // 界面接管输入时保留角色和相机目标，只暂停移动和切换。
    // locked 为 true 时收回移动控制，false 时交还给当前角色。
    public void SetGameplayLocked(bool locked)
    {
        gameplayLocked = locked;
        if (CurrentPlayer != null)
        {
            UpdateMoveInput();
        }
    }

    // 临时同行时仍允许移动，只暂停切换操控对象。
    internal void SetSwitchingLocked(bool locked)
    {
        switchingLocked = locked;
    }

    // 剧情演出可以把控制权交给仍在队伍里的指定角色。
    internal void SelectPlayer(PlayerMove player)
    {
        int index = System.Array.IndexOf(players, player);
        if (index < 0 || !player.gameObject.activeInHierarchy)
        {
            return;
        }

        players[currentPlayerIndex].SetMoveInput(null);
        currentPlayerIndex = index;
        GiveControlToCurrentPlayer();
        gameCamera.PreviousStateIsValid = false;
    }

    // 演出可以临时改变镜头目标，角色的操控权仍由切换系统管理。
    internal void SetCameraFocus(Transform target, bool snap = false)
    {
        gameCamera.Follow = target;
        if (snap)
        {
            gameCamera.PreviousStateIsValid = false;
        }
    }

    // F 对应的交互动作是否在本帧刚按下。
    public bool WasInteractPressed()
    {
        return input != null && input.PlayerMaps.Interact.WasPressedThisFrame();
    }

    // 是否在本帧按下上一选项键。
    public bool WasDialogueUpPressed()
    {
        return input != null && input.PlayerMaps.DialogueUp.WasPressedThisFrame();
    }

    // 是否在本帧按下下一选项键。
    public bool WasDialogueDownPressed()
    {
        return input != null && input.PlayerMaps.DialogueDown.WasPressedThisFrame();
    }

    // B 对应的背包动作是否在本帧刚按下。
    public bool WasInventoryPressed()
    {
        return input != null && input.PlayerMaps.Inventory.WasPressedThisFrame();
    }

    // Esc 对应的关闭动作是否在本帧刚按下。
    public bool WasInventoryCancelPressed()
    {
        return input != null && input.PlayerMaps.InventoryCancel.WasPressedThisFrame();
    }

    // 返回本帧的格子移动方向。行号从上向下增加。
    public Vector2Int ReadInventoryNavigation()
    {
        if (input == null)
        {
            return Vector2Int.zero;
        }

        if (WasDialogueUpPressed())
        {
            return Vector2Int.up * -1;
        }

        if (WasDialogueDownPressed())
        {
            return Vector2Int.up;
        }

        int left = input.PlayerMaps.InventoryLeft.WasPressedThisFrame() ? 1 : 0;
        int right = input.PlayerMaps.InventoryRight.WasPressedThisFrame() ? 1 : 0;
        return new Vector2Int(right - left, 0);
    }

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
        if (input == null)
        {
            return;
        }

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
        if (input != null)
        {
            input.Dispose();
        }
    }

    // 冷却结束后，把控制权交给列表里的下一个角色。
    // context 是输入系统的切换按键回调上下文。
    private void OnSwitchPressed(InputAction.CallbackContext context)
    {
        if (gameplayLocked || switchingLocked || players.Length < 2 || Time.time < nextSwitchTime)
            return;

        for (int offset = 1; offset < players.Length; offset++)
        {
            int next = (currentPlayerIndex + offset) % players.Length;
            if (players[next].gameObject.activeInHierarchy)
            {
                players[currentPlayerIndex].SetMoveInput(null);
                currentPlayerIndex = next;
                GiveControlToCurrentPlayer();
                nextSwitchTime = Time.time + switchCooldown;
                return;
            }
        }
    }

    // 交给当前角色移动输入，同时把它设为相机的跟随目标。
    private void GiveControlToCurrentPlayer()
    {
        UpdateMoveInput();
        SetCameraFocus(players[currentPlayerIndex].transform);
    }

    // 根据锁定状态将移动动作交给当前角色，或暂停其移动。
    private void UpdateMoveInput()
    {
        PlayerMove player = players[currentPlayerIndex];
        if (gameplayLocked)
        {
            player.SetMoveInput(null);
        }
        else
        {
            player.SetMoveInput(input.PlayerMaps.Move);
        }
    }
}
