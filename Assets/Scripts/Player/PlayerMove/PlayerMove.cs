using UnityEngine;
using UnityEngine.InputSystem;

// 处理俯视角角色在 XY 平面上的移动。
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    // 正常移动每秒的目标速度，由 Inspector 配置。
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    // 本角色的动态刚体，负责实际移动和碰撞。
    private Rigidbody2D body;
    // 当前角色收到的移动输入，交出控制权时为空。
    private InputAction moveInput;

    // 读取物理位置，避免把插值中的显示位置写入存档。
    internal Vector3 SavePosition => new Vector3(body.position.x, body.position.y, transform.position.z);

    // 同时更新刚体和显示位置，清掉旧速度后从读档点继续。
    internal void RestorePosition(Vector3 position)
    {
        body.position = position;
        body.velocity = Vector2.zero;
        transform.position = position;
    }

    // 缓存刚体，关闭重力并冻结 Z 轴旋转。
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    // 接收移动动作。传入 null 时，角色交出移动控制权。
    // action 是 Move 输入动作，传 null 时同时停止已有速度。
    public void SetMoveInput(InputAction action)
    {
        moveInput = action;
        if (moveInput == null && body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    // 在物理帧设置速度，让碰撞体决定角色能移动到哪里。
    private void FixedUpdate()
    {
        Vector2 direction = Vector2.zero;
        if (moveInput != null)
        {
            direction = moveInput.ReadValue<Vector2>();
        }

        body.velocity = direction * moveSpeed;
    }

    // 组件停用时清除最后一次移动留下的速度。
    private void OnDisable()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }
}
