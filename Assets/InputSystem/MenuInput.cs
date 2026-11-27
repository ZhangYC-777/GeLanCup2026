using UnityEngine.InputSystem;

// 弹窗确认与场景交互、对话推进使用不同按键。
internal static class MenuInput
{
    internal static bool WasConfirmPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
    }
}
