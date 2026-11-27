using UnityEngine.InputSystem;

// 共用 F 确认键，弹窗自行处理开场帧和焦点。
internal static class MenuInput
{
    internal static bool WasConfirmPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.fKey.wasPressedThisFrame;
    }
}
