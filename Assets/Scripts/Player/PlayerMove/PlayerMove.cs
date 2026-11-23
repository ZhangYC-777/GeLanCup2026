using UnityEngine;
using UnityEngine.InputSystem;

// 处理俯视角角色在 XY 平面上的移动。
public class PlayerMove : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    private InputAction moveInput;

    // 接收移动动作。传入 null 时，角色交出移动控制权。
    public void SetMoveInput(InputAction action)
    {
        moveInput = action;
    }

    // 每帧读取 Move 的二维方向，并在 XY 平面移动角色。
    private void Update()
    {
        if (moveInput == null)
            return;

        Vector2 direction = moveInput.ReadValue<Vector2>();
        transform.position += (Vector3)direction * (moveSpeed * Time.deltaTime);
    }
}
