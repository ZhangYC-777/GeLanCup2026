using System.Collections.Generic;
using UnityEngine;

// 在同一房间自由活动超过限定时间后，生成追猎者。
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
internal sealed class TimedRoomHunter : MonoBehaviour
{
    internal const float Delay = 45f;

    [SerializeField] private SceneSave sceneSave;
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private SafeRoomSequence firstFloorArrival;
    [SerializeField] private PuzzleRoomSequence puzzle;
    [SerializeField] private SecondFloorArrival secondFloorArrival;
    [SerializeField] private SecondFloorExploration exploration;
    [SerializeField] private Sprite sprite;
    [SerializeField] private Material spriteMaterial;
    [SerializeField] private AudioClip footsteps;

    private const float Speed = 2.8f;
    private const float Radius = .19f;
    private const float StrikeDistance = .65f;
    private const float StrikeCooldown = 2.5f;
    private const float TrailSpacing = .35f;

    private readonly ContactFilter2D solidFilter = new ContactFilter2D { useTriggers = false };
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private readonly List<Vector2> trail = new List<Vector2>();
    private int room = -1;
    private float roomSeconds;
    private float strikeReadyAt;
    private float nextFootstep;
    private float nextSpawnAttempt;
    private SpriteRenderer hunter;
    private AudioSource steps;
    private Transform followedPlayer;

    private void LateUpdate()
    {
        Transform player = party.CurrentPlayer;
        if (player == null || sceneSave.IsLoading || sceneSave.PartySan.CurrentSan <= 0)
        {
            HideHunter();
            return;
        }

        Vector2 position = player.position;
        int nextRoom = GetRoom(position);
        if (room != nextRoom)
        {
            room = nextRoom;
            roomSeconds = 0f;
            nextSpawnAttempt = 0f;
            HideHunter();
        }

        if (room < 0 || party.IsGameplayLocked || dialogue.IsPlaying ||
            firstFloorArrival != null && !firstFloorArrival.DoorsReadyForExploration ||
            puzzle != null && puzzle.BlocksTimedHunter ||
            secondFloorArrival != null && secondFloorArrival.BlocksTimedHunter ||
            exploration != null && exploration.BlocksTimedHunter)
        {
            HideHunter();
            return;
        }

        roomSeconds += Time.deltaTime;
        if (roomSeconds < Delay)
        {
            return;
        }

        if (hunter == null || !hunter.enabled)
        {
            if (Time.time >= nextSpawnAttempt)
            {
                nextSpawnAttempt = Time.time + .25f;
                TrySpawn(player, position);
            }
            return;
        }

        Chase(player, position);
    }

    private int GetRoom(Vector2 position)
    {
        return visibility == null ? 0 : visibility.GetHunterRoom(position, room);
    }

    private void TrySpawn(Transform player, Vector2 target)
    {
        for (float distance = 3.25f; distance >= .64f; distance -= .65f)
        {
            for (int index = 0; index < 16; index++)
            {
                float angle = index * Mathf.PI / 8f;
                Vector2 spawn = target + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * distance;
                Vector2 approach = Vector2.MoveTowards(target, spawn, StrikeDistance * .7f);
                if (GetRoom(spawn) != room || !HasClearPath(spawn, approach))
                {
                    continue;
                }

                if (hunter == null)
                {
                    var actor = new GameObject("Timed Room Hunter");
                    actor.transform.SetParent(transform, false);
                    hunter = actor.AddComponent<SpriteRenderer>();
                    hunter.sprite = sprite;
                    if (spriteMaterial != null)
                    {
                        hunter.sharedMaterial = spriteMaterial;
                    }
                    steps = actor.AddComponent<AudioSource>();
                    steps.playOnAwake = false;
                    steps.spatialBlend = 0f;
                    steps.pitch = .78f;
                }

                hunter.transform.position = spawn;
                hunter.enabled = true;
                UpdateSprite(target - spawn);
                followedPlayer = player;
                trail.Clear();
                trail.Add(target);
                strikeReadyAt = Time.time + .8f;
                nextFootstep = Time.time;
                PlayFootstep();
                return;
            }
        }
    }

    private void Chase(Transform player, Vector2 target)
    {
        if (followedPlayer != player)
        {
            HideHunter();
            return;
        }
        if (trail.Count == 0 || (trail[trail.Count - 1] - target).sqrMagnitude >= TrailSpacing * TrailSpacing)
        {
            trail.Add(target);
        }

        Vector2 start = hunter.transform.position;
        Vector2 approach = Vector2.MoveTowards(target, start, StrikeDistance * .7f);
        bool direct = HasClearPath(start, approach);
        if (direct)
        {
            trail.Clear();
            trail.Add(target);
        }
        else
        {
            // 视线被墙挡住时，沿同一角色最近经过的转角推进。
            int reachable = FindReachableTrailPoint(start);
            if (reachable < 0)
            {
                return;
            }
            trail.RemoveRange(0, reachable);
        }

        Vector2 destination = direct ? approach : trail[0];
        Vector2 next = Vector2.MoveTowards(start, destination, Speed * Time.deltaTime);
        if (GetRoom(next) == room)
        {
            hunter.transform.position = next;
            UpdateSprite(next - start);
            if ((next - start).sqrMagnitude > .00001f && Time.time >= nextFootstep)
            {
                PlayFootstep();
            }
        }

        if (direct && Time.time >= strikeReadyAt && (target - next).sqrMagnitude <= StrikeDistance * StrikeDistance)
        {
            strikeReadyAt = Time.time + StrikeCooldown;
            sceneSave.PartySan.TakeDamage(PartySan.StandardDamage);
        }
    }

    private int FindReachableTrailPoint(Vector2 start)
    {
        int first = Mathf.Max(0, trail.Count - 24);
        for (int index = trail.Count - 1; index >= first; index--)
        {
            if (HasClearPath(start, trail[index]))
            {
                return index;
            }
        }
        return first > 0 && HasClearPath(start, trail[0]) ? 0 : -1;
    }

    private bool HasClearPath(Vector2 from, Vector2 to)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        if (distance < .001f)
        {
            return true;
        }

        int count = Physics2D.CircleCast(from, Radius, offset / distance, solidFilter, hits, distance);
        for (int index = 0; index < count; index++)
        {
            if (hits[index].collider.GetComponentInParent<PlayerMove>() == null)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateSprite(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > .01f)
        {
            hunter.flipX = direction.x < 0f;
        }
        hunter.sortingOrder = 85 - Mathf.RoundToInt(hunter.transform.position.y * 100f);
    }

    private void PlayFootstep()
    {
        if (footsteps != null)
        {
            steps.PlayOneShot(footsteps, .36f);
        }
        nextFootstep = Time.time + .54f;
    }

    private void HideHunter()
    {
        if (hunter != null && hunter.enabled)
        {
            hunter.enabled = false;
            steps.Stop();
            trail.Clear();
        }
    }
}
