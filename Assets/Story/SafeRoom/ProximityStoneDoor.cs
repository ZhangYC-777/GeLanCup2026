using System.Collections.Generic;
using UnityEngine;

// 石门升起后响应玩家接近。门洞内有人时，保持开启。
[DisallowMultipleComponent]
public sealed class ProximityStoneDoor : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private Collider2D approachArea;
    [SerializeField] private Collider2D doorwaySafetyArea;
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private Collider2D closedDoorCollider;
    [SerializeField] private Transform risingVisuals;
    [SerializeField] private GameObject wallCover;
    [SerializeField] private PlayerHit doorInspection;
    [SerializeField, Min(0.1f)] private float riseDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float riseSeconds = 0.85f;

    private static readonly int OpenParameter = Animator.StringToHash("open");
    private static readonly int OpenState = Animator.StringToHash("DoorNSOpen");
    private readonly List<Collider2D> occupants = new List<Collider2D>(6);
    private readonly ContactFilter2D occupantFilter = new ContactFilter2D { useTriggers = false };
    private Vector3 raisedPosition;
    private float remainingRise;
    private bool rising;
    private bool raised;

    private void Awake()
    {
        raisedPosition = risingVisuals.localPosition;
        risingVisuals.localPosition = raisedPosition + Vector3.down * riseDistance;
        if (doorInspection != null)
        {
            doorInspection.enabled = false;
        }
    }

    public void BeginRise()
    {
        if (raised || rising)
        {
            return;
        }

        wallCover.SetActive(false);
        remainingRise = riseSeconds;
        rising = true;
    }

    // 读档恢复石门的演出状态。有人存档在门洞时，先让出通路。
    public void RestoreRaised()
    {
        wallCover.SetActive(false);
        FinishRise();
        if (IsDoorwayOccupied())
        {
            doorAnimator.SetBool(OpenParameter, true);
            doorAnimator.Play(OpenState, 0, 0f);
            doorAnimator.Update(0f);
            closedDoorCollider.enabled = false;
        }
    }

    private void Update()
    {
        if (rising)
        {
            remainingRise = Mathf.Max(0f, remainingRise - Time.deltaTime);
            risingVisuals.localPosition = raisedPosition + Vector3.down * (riseDistance * remainingRise / riseSeconds);
            if (remainingRise == 0f)
            {
                FinishRise();
            }
        }

        if (!raised)
        {
            return;
        }

        Transform player = switchPlayer.CurrentPlayer;
        bool approaching = player != null && approachArea.OverlapPoint(player.position);
        bool shouldOpen = approaching || IsDoorwayOccupied();
        doorAnimator.SetBool(OpenParameter, shouldOpen);
        closedDoorCollider.enabled = !shouldOpen || doorAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != OpenState;
    }

    private void FinishRise()
    {
        risingVisuals.localPosition = raisedPosition;
        rising = false;
        raised = true;
        if (doorInspection != null)
        {
            doorInspection.enabled = true;
        }
    }

    private bool IsDoorwayOccupied()
    {
        doorwaySafetyArea.OverlapCollider(occupantFilter, occupants);
        foreach (Collider2D occupant in occupants)
        {
            if (occupant.attachedRigidbody != null && occupant.attachedRigidbody.TryGetComponent<PlayerMove>(out _))
            {
                return true;
            }
        }

        return false;
    }
}
