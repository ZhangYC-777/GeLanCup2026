using UnityEngine;
using UnityEngine.InputSystem;

// 处理俯视角角色在 XY 平面上的移动。
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    // 正常移动每秒的目标速度，由 Inspector 配置。
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField] private SpriteRenderer characterSprite;
    [SerializeField] private PlayerWalkSprites walkSprites;

    private const float MinimumMotionSquared = 0.000001f;
    private const float WalkStopDelay = 0.06f;

    // 本角色的动态刚体，负责实际移动和碰撞。
    private Rigidbody2D body;
    // 当前角色收到的移动输入，交出控制权时为空。
    private InputAction moveInput;
    private Vector2 moveDirection;
    private Vector2 sampledPosition;
    private Vector2 physicsMotion;
    private Vector2 storyMotion;
    private double sampledFixedTime;
    private PlayerWalkSprites.Facing facing;
    private bool facingHorizontally;
    private bool walking;
    private bool lastMotionWasScripted;
    private float walkElapsed;
    private float stoppedElapsed;

    // 读取物理位置，避免把插值中的显示位置写入存档。
    internal Vector3 SavePosition => new Vector3(body.position.x, body.position.y, transform.position.z);

    // 同时更新刚体和显示位置，清掉旧速度后从读档点继续。
    internal void RestorePosition(Vector3 position)
    {
        PlaceAt(position);
        storyMotion = Vector2.zero;
        ShowIdle();
    }

    // 剧情行走和跟随每帧给出目标位置，位移同时用于判定朝向和步态。
    internal void MoveForStory(Vector3 position)
    {
        storyMotion += (Vector2)position - body.position;
        PlaceAt(position);
    }

    // 剧情定位既更新刚体，也保持演出脚本读取到的位置一致。
    private void PlaceAt(Vector3 position)
    {
        body.position = position;
        body.velocity = Vector2.zero;
        transform.position = position;
        sampledPosition = body.position;
        physicsMotion = Vector2.zero;
    }

    // 剧情里被脚本带着走的角色要能穿过柱子一类的布景，否则物理会把刚体顶回去，看着像被卡住。
    internal void SetColliderEnabled(bool value)
    {
        foreach (Collider2D shape in GetComponents<Collider2D>())
        {
            shape.enabled = value;
        }
    }

    // 缓存刚体，关闭重力并冻结 Z 轴旋转。
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    // 角色重新出现时从当前位置采样，避免把显隐前的位置变化当成走路。
    private void OnEnable()
    {
        sampledPosition = body.position;
        sampledFixedTime = Time.fixedTimeAsDouble;
        physicsMotion = Vector2.zero;
        storyMotion = Vector2.zero;
        ShowIdle();
    }

    // 接收移动动作。传入 null 时，角色交出移动控制权。
    // action 是 Move 输入动作，传 null 时同时停止已有速度。
    public void SetMoveInput(InputAction action)
    {
        moveInput = action;
        if (moveInput == null && body != null)
        {
            body.velocity = Vector2.zero;
            moveDirection = Vector2.zero;
        }
    }

    // 在物理帧设置速度，让碰撞体决定角色能移动到哪里。
    private void FixedUpdate()
    {
        moveDirection = moveInput != null ? moveInput.ReadValue<Vector2>() : Vector2.zero;
        body.velocity = moveDirection * moveSpeed;
    }

    // 物理帧之间沿用最近一次位移，避免渲染较快时在走路和待机之间闪动。
    private void LateUpdate()
    {
        if (characterSprite == null || walkSprites == null)
        {
            return;
        }

        double fixedTime = Time.fixedTimeAsDouble;
        if (sampledFixedTime != fixedTime)
        {
            Vector2 position = body.position;
            physicsMotion = position - sampledPosition;
            sampledPosition = position;
            sampledFixedTime = fixedTime;
        }

        bool scripted = storyMotion.sqrMagnitude > MinimumMotionSquared;
        bool hasMoveInput = moveDirection != Vector2.zero;
        Vector2 motion = scripted ? storyMotion : hasMoveInput ? physicsMotion : Vector2.zero;
        storyMotion = Vector2.zero;
        if (motion.sqrMagnitude <= MinimumMotionSquared)
        {
            if (!walking)
            {
                return;
            }

            // 跟随和擦墙时可能短暂没有位移，先保留当前步态。松键则立即停步。
            stoppedElapsed += lastMotionWasScripted ? Time.unscaledDeltaTime : Time.deltaTime;
            if ((!lastMotionWasScripted && !hasMoveInput) || stoppedElapsed >= WalkStopDelay)
            {
                ShowIdle();
            }
            return;
        }

        stoppedElapsed = 0f;
        lastMotionWasScripted = scripted;

        // 接近 45 度时保持原轴，减少斜走和沿墙滑动时的朝向切换。
        float facingThreshold = facingHorizontally ? 0.9f : 1.1f;
        facingHorizontally = Mathf.Abs(motion.x) > Mathf.Abs(motion.y) * facingThreshold;
        if (facingHorizontally)
        {
            facing = motion.x > 0f ? PlayerWalkSprites.Facing.Right : PlayerWalkSprites.Facing.Left;
        }
        else
        {
            facing = motion.y > 0f ? PlayerWalkSprites.Facing.Up : PlayerWalkSprites.Facing.Left;
        }

        if (walking)
        {
            walkElapsed += scripted ? Time.unscaledDeltaTime : Time.deltaTime;
        }
        walking = true;
        ShowFrame();
    }

    // 停步时保留最后的朝向，下一次起步从第一张走路帧开始。
    private void ShowIdle()
    {
        walking = false;
        lastMotionWasScripted = false;
        walkElapsed = 0f;
        stoppedElapsed = 0f;
        ShowFrame();
    }

    // 只换人物贴图。演出仍可以控制渲染器的颜色和显隐。
    private void ShowFrame()
    {
        if (characterSprite == null || walkSprites == null)
        {
            return;
        }

        Sprite frame = walkSprites.GetFrame(facing, walking, walkElapsed);
        if (frame != null && characterSprite.sprite != frame)
        {
            characterSprite.sprite = frame;
        }
    }

    // 组件停用时清除最后一次移动留下的速度。
    private void OnDisable()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }

        moveDirection = Vector2.zero;
        ShowIdle();
    }
}
