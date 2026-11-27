using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// 从解谜房开启的一局面包迷宫。地图、体力和暂停状态都只存在于小游戏内。
[DisallowMultipleComponent]
public sealed class BreadTrial : MonoBehaviour
{
    private enum State { Closed, Ready, Playing, Paused, Lost, Won }

    [SerializeField] private Sprite[] floors;
    [SerializeField] private Sprite wall;
    [SerializeField] private Sprite exit;
    [SerializeField] private Sprite bread;
    [SerializeField] private Sprite glow;
    [SerializeField] private Shader flashlightShader;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip heartbeat;

    private const float MoveSpeed = 4.3f;
    private const float DashSpeed = 10.5f;
    private const float DashCooldown = 2f;
    private const float RunnerRadius = .24f;
    private static readonly Vector2 WorldOffset = new Vector2(256f, 256f);
    private static readonly Color ShadowColor = new Color32(129, 37, 51, 255);

    private readonly List<SpriteRenderer> loaves = new List<SpriteRenderer>(BreadMazeLayout.BreadCount);
    private readonly List<Tile> runtimeTiles = new List<Tile>();
    private State state;
    private Action onWon;
    private Action<bool> onClosed;
    private Sprite runnerSprite;
    private Color runnerTint;
    private BreadTrialView view;
    private Camera mazeCamera;
    private BreadTrialLight light;
    private RenderTexture target;
    private Transform world;
    private AudioSource audioSource;
    private BreadMazeLayout layout;
    private SpriteRenderer runner;
    private SpriteRenderer shadow;
    private int[,] exitDistances;
    private int[,] shadowDistances;
    private int health;
    private int collected;
    private int hits;
    private int openedFrame;
    private float elapsed;
    private float cooldown;
    private float dashTime;
    private float invulnerability;
    private float shadowStun;
    private float hearTime;
    private float repathTime;
    private float heartbeatTime;
    private Vector2 position;
    private Vector2 facing;
    private Vector2 velocity;
    private Vector2Int shadowCell;
    private Vector2Int shadowNext;
    private Vector2 shadowPosition;
    private bool chasing;

    internal bool Begin(SpriteRenderer avatar, Action celebrate, Action<bool> close)
    {
        if (state != State.Closed || avatar == null || avatar.sprite == null || floors.Length == 0 || wall == null ||
            exit == null || bread == null || glow == null || flashlightShader == null)
        {
            Debug.LogError("面包迷宫缺少场景引用，或已有一局正在运行。", this);
            return false;
        }

        runnerSprite = avatar.sprite;
        runnerTint = avatar.color;
        onWon = celebrate;
        onClosed = close;
        var canvas = new GameObject("Bread Trial Overlay", typeof(RectTransform));
        canvas.transform.SetParent(transform, false);
        view = canvas.AddComponent<BreadTrialView>();
        view.Build();

        var cameraObject = new GameObject("Bread Trial Camera");
        cameraObject.transform.SetParent(transform, false);
        mazeCamera = cameraObject.AddComponent<Camera>();
        mazeCamera.orthographic = true;
        mazeCamera.orthographicSize = 5.4f;
        mazeCamera.clearFlags = CameraClearFlags.SolidColor;
        mazeCamera.backgroundColor = new Color32(13, 10, 16, 255);
        mazeCamera.allowHDR = false;
        mazeCamera.allowMSAA = false;
        mazeCamera.depth = -10f;
        light = cameraObject.AddComponent<BreadTrialLight>();
        light.Configure(flashlightShader, mazeCamera, WorldOffset);

        audioSource = cameraObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        PrepareRun();
        ChangeState(State.Ready);
        view.ShowMenu("面包迷宫", "沿着手电的光找出口。捡到面包可恢复体力，冲刺可能引来影子。",
            "开始  Enter", StartPlaying, "返回解谜房  Esc", Close);
        return true;
    }

    private void Update()
    {
        if (state == State.Closed) return;

        ResizeTarget();
        if (Time.frameCount == openedFrame) return;

        Keyboard keys = Keyboard.current;
        bool confirm = MenuInput.WasConfirmPressed();
        bool escape = keys != null && keys.escapeKey.wasPressedThisFrame;
        bool retry = keys != null && keys.rKey.wasPressedThisFrame;
        bool leave = keys != null && keys.qKey.wasPressedThisFrame;

        switch (state)
        {
            case State.Ready:
                if (escape) Close();
                else if (confirm) StartPlaying();
                return;
            case State.Paused:
                if (leave) Close();
                else if (retry) Restart();
                else if (escape || confirm) StartPlaying();
                return;
            case State.Lost:
                if (escape || leave) Close();
                else if (retry || confirm) Restart();
                return;
            case State.Won:
                if (escape || confirm) Close();
                return;
        }

        if (escape)
        {
            ChangeState(State.Paused);
            view.ShowMenu("已暂停", "Esc 继续    ·    R 重新开始",
                "继续  Esc", StartPlaying, "返回解谜房  Q", Close);
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, .04f);
        elapsed += dt;
        UpdateRunner(ReadDirection(keys), keys != null &&
            (keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame), dt);
        UpdateShadow(dt);
        if (health <= 0)
        {
            ChangeState(State.Lost);
            view.ShowMenu("影子追上了你", "可以重试，或回到桌边再试。",
                "重试  R", Restart, "返回解谜房  Esc", Close);
        }
        else if ((position - BreadMazeLayout.Center(BreadMazeLayout.Exit)).sqrMagnitude < .28f)
        {
            ChangeState(State.Won);
            onWon();
            Play(winSound, .62f);
            view.ShowMenu("到达出口", $"用时 {TimeSpan.FromSeconds(elapsed):mm\\:ss}    ·    面包 {collected} / {BreadMazeLayout.BreadCount}    ·    被撞 {hits} 次",
                "返回解谜房  Enter", Close, null, null);
        }

        UpdateWorldView(dt);
    }

    private void PrepareRun()
    {
        ReleaseTerrain();
        layout = new BreadMazeLayout(UnityEngine.Random.Range(-1000000000, 1000000000));
        exitDistances = layout.DistancesFrom(BreadMazeLayout.Exit);
        shadowDistances = null;

        position = BreadMazeLayout.Center(BreadMazeLayout.Start);
        facing = layout.InitialFacing;
        velocity = Vector2.zero;
        shadowCell = shadowNext = layout.MonsterStart;
        shadowPosition = BreadMazeLayout.Center(shadowCell);
        health = 100;
        collected = hits = 0;
        elapsed = cooldown = dashTime = invulnerability = shadowStun = hearTime = repathTime = heartbeatTime = 0f;
        chasing = false;

        world = new GameObject("Bread Trial World").transform;
        world.SetParent(transform, false);
        world.position = WorldOffset;
        BuildTerrain();

        Vector2 exitPosition = BreadMazeLayout.Center(BreadMazeLayout.Exit);
        PlaceSprite("Exit Glow", glow, exitPosition, new Vector2(2.4f, 1.7f), new Color32(114, 237, 202, 210), 1);
        PlaceSprite("Exit", exit, exitPosition, new Vector2(1.15f, 1.2f), new Color32(180, 255, 230, 255), 2);
        foreach (Vector2Int cell in layout.BreadCells)
        {
            Vector2 center = BreadMazeLayout.Center(cell);
            SpriteRenderer loaf = PlaceSprite("Cream Bread", bread, center, new Vector2(.64f, .57f), Color.white, 3);
            loaves.Add(loaf);
        }

        shadow = PlaceSprite("Shadow", runnerSprite, shadowPosition, new Vector2(.95f, 1.22f), ShadowColor, 4);
        PlaceSprite("Shadow Aura", glow, Vector2.zero, new Vector2(1.7f, 1.1f), new Color32(244, 49, 63, 155), 2)
            .transform.SetParent(shadow.transform, false);
        runner = PlaceSprite("Player", runnerSprite, position, new Vector2(.83f, 1.14f), runnerTint, 5);

        ResizeTarget();
        mazeCamera.transform.position = new Vector3(WorldOffset.x + position.x, WorldOffset.y + position.y, -10f);
        UpdateWorldView(0f);
    }

    private void BuildTerrain()
    {
        var grid = world.gameObject.AddComponent<Grid>();
        grid.cellSize = Vector3.one * BreadMazeLayout.CellSize;
        var tileRoot = new GameObject("Dungeon Tiles", typeof(Tilemap), typeof(TilemapRenderer));
        tileRoot.transform.SetParent(world, false);
        tileRoot.transform.localPosition = Vector3.one * (-BreadMazeLayout.CellSize * .5f);
        Tilemap map = tileRoot.GetComponent<Tilemap>();
        Tile wallTile = MakeTile(wall, new Color32(155, 131, 129, 255));
        var floorTiles = new Tile[floors.Length];
        for (int i = 0; i < floors.Length; i++)
            floorTiles[i] = MakeTile(floors[i], new Color32(208, 195, 172, 255));

        for (int y = 0; y < BreadMazeLayout.Size; y++)
        {
            for (int x = 0; x < BreadMazeLayout.Size; x++)
            {
                var cell = new Vector3Int(x, y);
                Tile tile = layout.IsOpen((Vector2Int)cell) ? floorTiles[(x * 3 + y * 7) % floors.Length] : wallTile;
                map.SetTile(cell, tile);
            }
        }
    }

    private Tile MakeTile(Sprite sprite, Color tint)
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = tint;
        tile.colliderType = Tile.ColliderType.None;
        tile.transform = Matrix4x4.Scale(new Vector3(
            BreadMazeLayout.CellSize / sprite.bounds.size.x, BreadMazeLayout.CellSize / sprite.bounds.size.y, 1f));
        runtimeTiles.Add(tile);
        return tile;
    }

    private SpriteRenderer PlaceSprite(string name, Sprite sprite, Vector2 at, Vector2 size, Color color, int order)
    {
        var item = new GameObject(name);
        item.transform.SetParent(world, false);
        item.transform.localPosition = at;
        var renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        float scale = Mathf.Min(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y);
        item.transform.localScale = Vector3.one * scale;
        return renderer;
    }

    private static Vector2 ReadDirection(Keyboard keys)
    {
        if (keys == null) return Vector2.zero;

        int right = keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0;
        int left = keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0;
        int up = keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0;
        int down = keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0;
        return new Vector2(right - left, up - down).normalized;
    }

    private void UpdateRunner(Vector2 direction, bool dashPressed, float dt)
    {
        cooldown = Mathf.Max(0, cooldown - dt);
        invulnerability = Mathf.Max(0, invulnerability - dt);
        if (dashTime <= 0f && direction != Vector2.zero)
            facing = direction;
        if (dashPressed && cooldown <= 0f)
        {
            dashTime = .16f;
            cooldown = DashCooldown;
            Play(dashSound, .4f);
        }

        Vector2 desired = dashTime > 0f ? facing * DashSpeed : direction * MoveSpeed;
        velocity = Vector2.MoveTowards(velocity, desired, 38f * dt);
        if (dashTime > 0f)
            velocity = desired;
        dashTime = Mathf.Max(0, dashTime - dt);
        MoveRunner(velocity * dt);

        for (int i = loaves.Count - 1; i >= 0; i--)
        {
            SpriteRenderer loaf = loaves[i];
            Vector2 center = (Vector2)loaf.transform.localPosition;
            if ((position - center).sqrMagnitude > .31f) continue;

            Destroy(loaf.gameObject);
            loaves.RemoveAt(i);
            health = Mathf.Min(100, health + 25);
            collected++;
            Play(pickupSound, .5f);
        }
    }

    private void MoveRunner(Vector2 offset)
    {
        Vector2 horizontal = position + Vector2.right * offset.x;
        if (CanStand(horizontal))
            position = horizontal;
        Vector2 vertical = position + Vector2.up * offset.y;
        if (CanStand(vertical))
            position = vertical;
    }

    private bool CanStand(Vector2 at)
    {
        Vector2Int min = BreadMazeLayout.CellAt(at - Vector2.one * RunnerRadius);
        Vector2Int max = BreadMazeLayout.CellAt(at + Vector2.one * RunnerRadius);
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                if (!layout.IsOpen(new Vector2Int(x, y)))
                    return false;
            }
        }
        return true;
    }

    private void UpdateShadow(float dt)
    {
        if (elapsed < 3f) return;

        float distance = Vector2.Distance(position, shadowPosition);
        bool canHear = distance <= (dashTime > 0f ? 23f : 16f);
        hearTime = canHear ? 3.2f : hearTime - dt;
        bool wasChasing = chasing;
        chasing = hearTime > 0f;

        heartbeatTime -= dt;
        if (heartbeatTime <= 0f && distance < 16f)
        {
            float proximity = 1f - Mathf.Clamp01(distance / 16f);
            heartbeatTime = Mathf.Lerp(1.12f, .28f, proximity);
            Play(heartbeat, Mathf.Lerp(.1f, .6f, proximity));
        }

        if (shadowStun > 0f)
        {
            shadowStun -= dt;
            return;
        }

        repathTime -= dt;
        if (chasing && canHear && (repathTime <= 0f || !wasChasing))
        {
            shadowDistances = layout.DistancesFrom(BreadMazeLayout.CellAt(position));
            repathTime = .3f;
        }
        else if (!chasing && (shadowDistances == null || wasChasing || shadowDistances[shadowCell.x, shadowCell.y] == 1))
        {
            shadowDistances = layout.DistancesFrom(layout.ChoosePatrolCell());
        }

        if (shadowCell == shadowNext)
            shadowNext = layout.StepTowards(shadowCell, shadowDistances);

        Vector2 next = BreadMazeLayout.Center(shadowNext);
        shadowPosition = Vector2.MoveTowards(shadowPosition, next, (chasing ? 3.05f : 1.8f) * dt);
        if (shadowPosition == next)
            shadowCell = shadowNext;

        if (distance <= .72f && invulnerability <= 0f)
        {
            health = Mathf.Max(0, health - 20);
            hits++;
            invulnerability = 1.3f;
            shadowStun = .85f;
            velocity = (position - shadowPosition).normalized * 7f;
            Play(hurtSound, .5f);
        }
    }

    private void UpdateWorldView(float dt)
    {
        runner.transform.localPosition = position;
        if (Mathf.Abs(facing.x) > .05f)
            runner.flipX = facing.x < 0f;
        runner.color = invulnerability > 0 && Mathf.FloorToInt(invulnerability * 14) % 2 == 0
            ? new Color32(255, 117, 110, 135) : runnerTint;
        shadow.transform.localPosition = shadowPosition;
        Vector2 cameraTarget = WorldOffset + position;
        Vector2 current = mazeCamera.transform.position;
        Vector2 next = Vector2.Lerp(current, cameraTarget, 1f - Mathf.Exp(-10f * dt));
        mazeCamera.transform.position = new Vector3(next.x, next.y, -10f);

        float shadowDistance = Vector2.Distance(position, shadowPosition);
        float danger = Mathf.Max(1f - health / 100f, chasing ? 1f - Mathf.Clamp01(shadowDistance / 12f) : 0f);
        light.SetState(layout, position, facing, danger);
        Vector2Int cell = BreadMazeLayout.CellAt(position);
        view.UpdateHud(health, collected, (exitDistances[cell.x, cell.y] - 1) * BreadMazeLayout.CellSize,
            shadowDistance, chasing, cooldown);
    }

    private void ResizeTarget()
    {
        const int height = 540;
        int width = Mathf.Clamp(Mathf.RoundToInt(height * Screen.width / (float)Mathf.Max(1, Screen.height)), 440, 1920);
        if (target != null && target.width == width) return;

        ReleaseTarget();
        target = new RenderTexture(width, height, 0) { name = "Bread Trial View" };
        mazeCamera.targetTexture = target;
        mazeCamera.aspect = width / (float)height;
        view.SetTexture(target);
    }

    private void Restart()
    {
        PrepareRun();
        StartPlaying();
    }

    private void StartPlaying()
    {
        ChangeState(State.Playing);
        view.HideMenu();
    }

    private void ChangeState(State next)
    {
        state = next;
        openedFrame = Time.frameCount;
    }

    private void Close()
    {
        if (state == State.Closed) return;

        bool won = state == State.Won;
        Action<bool> callback = onClosed;
        ReleaseTrial();
        callback(won);
    }

    private void ReleaseTerrain()
    {
        if (world != null)
        {
            world.gameObject.SetActive(false);
            Destroy(world.gameObject);
        }
        foreach (Tile tile in runtimeTiles)
            Destroy(tile);
        runtimeTiles.Clear();
        loaves.Clear();
    }

    private void ReleaseTarget()
    {
        if (target == null) return;

        mazeCamera.targetTexture = null;
        target.Release();
        Destroy(target);
        target = null;
    }

    private void ReleaseTrial()
    {
        ChangeState(State.Closed);
        ReleaseTerrain();
        ReleaseTarget();
        if (view != null)
        {
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }
        if (mazeCamera != null)
        {
            mazeCamera.enabled = false;
            Destroy(mazeCamera.gameObject);
        }
        onWon = null;
        onClosed = null;
    }

    private void OnDisable()
    {
        if (state != State.Closed)
            ReleaseTrial();
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

}
