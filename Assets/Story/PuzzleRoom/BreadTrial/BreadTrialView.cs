using System;
using UnityEngine;
using UnityEngine.UI;

// 小游戏 HUD 和菜单只覆盖当前场景，关闭后立即露出原来的解谜房。
internal sealed class BreadTrialView : MonoBehaviour
{
    private Font font;
    private RawImage backdrop;
    private RectTransform modal;
    private Text heading;
    private Text message;
    private Button primary;
    private Button secondary;
    private Text primaryLabel;
    private Text secondaryLabel;
    private Text stamina;
    private Text objective;
    private Text danger;
    private Text dash;
    private Image staminaFill;

    internal void Build()
    {
        font = DialogueView.FindDialogueFont();
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        var viewport = Box(transform, "Maze View", Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(viewport);
        backdrop = viewport.gameObject.AddComponent<RawImage>();
        backdrop.raycastTarget = true;

        var upperLeft = Box(transform, "Stamina", new Vector2(0.5f, 1f), new Vector2(-445, -56), new Vector2(222, 70));
        GameUiTheme.Frame(upperLeft);
        stamina = Label(upperLeft, "Stamina Text", 21, GameUiTheme.Paper, new Vector2(0, 10), new Vector2(206, 34));
        RectTransform bar = Box(upperLeft, "Stamina Meter", Vector2.one * .5f, new Vector2(0, -18), new Vector2(182, 17));
        RectTransform well = GameUiTheme.MeterTrack(bar);
        var fill = GameUiTheme.Box(well, "Stamina Fill");
        GameUiTheme.Place(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        staminaFill = GameUiTheme.Fill(fill, GameUiTheme.Vital);

        var upperCenter = Box(transform, "Objectives", new Vector2(.5f, 1f), new Vector2(0, -56), new Vector2(572, 70));
        GameUiTheme.Frame(upperCenter);
        objective = Label(upperCenter, "Objective Text", 22, GameUiTheme.Paper, new Vector2(0, 12), new Vector2(550, 34));
        danger = Label(upperCenter, "Danger Text", 17, GameUiTheme.Muted, new Vector2(0, -16), new Vector2(550, 28));

        var upperRight = Box(transform, "Dash", new Vector2(.5f, 1f), new Vector2(445, -56), new Vector2(222, 70));
        GameUiTheme.Frame(upperRight);
        dash = Label(upperRight, "Dash Text", 21, GameUiTheme.Paper, Vector2.zero, upperRight.sizeDelta);

        var footer = Box(transform, "Controls", new Vector2(.5f, 0f), new Vector2(0, 36), new Vector2(622, 38));
        GameUiTheme.Frame(footer);
        Label(footer, "Key Hints", 19, GameUiTheme.Muted, Vector2.zero, footer.sizeDelta).text =
            "WASD / 方向键 移动     Shift / 空格 冲刺     Esc 暂停";

        modal = Box(transform, "Menu Backdrop", Vector2.one * .5f, Vector2.zero, Vector2.zero);
        Stretch(modal);
        Fill(modal, GameUiTheme.Shade).raycastTarget = true;
        var card = Box(modal, "Menu Card", Vector2.one * .5f, Vector2.zero, new Vector2(650, 308));
        GameUiTheme.Frame(card);
        heading = Label(card, "Title", 43, GameUiTheme.Paper, new Vector2(0, 62), new Vector2(610, 64));
        heading.fontStyle = FontStyle.Bold;
        message = Label(card, "Message", 23, GameUiTheme.Muted, new Vector2(0, -8), new Vector2(598, 64));
        primary = MenuButton(card, "Confirm", new Vector2(-133, -101), out primaryLabel);
        secondary = MenuButton(card, "Return", new Vector2(133, -101), out secondaryLabel);
    }

    internal void SetTexture(Texture texture) => backdrop.texture = texture;

    internal void UpdateHud(int health, int bread, float exitDistance, float shadowDistance, bool chasing, float cooldown)
    {
        stamina.text = $"体力  {health} / 100";
        staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(health / 100f), 1f);
        staminaFill.color = health <= 40 ? GameUiTheme.Danger : GameUiTheme.Vital;
        objective.text = $"出口 {exitDistance:0.0} m       面包 {bread} / {BreadMazeLayout.BreadCount}";
        danger.text = chasing ? $"影子正在追你  ·  {shadowDistance:0.0} m" : $"影子距离  {shadowDistance:0.0} m";
        danger.color = chasing ? GameUiTheme.Danger : GameUiTheme.Muted;
        dash.text = cooldown <= 0f ? "冲刺  就绪" : $"冲刺  {cooldown:0.0} s";
    }

    internal void ShowMenu(string title, string description, string confirm, Action onConfirm, string cancel, Action onCancel)
    {
        heading.text = title;
        message.text = description;
        primaryLabel.text = confirm;
        primary.onClick.RemoveAllListeners();
        primary.onClick.AddListener(() => onConfirm());
        primary.GetComponent<RectTransform>().anchoredPosition = new Vector2(onCancel == null ? 0 : -133, -101);
        secondary.gameObject.SetActive(onCancel != null);
        secondary.onClick.RemoveAllListeners();
        if (onCancel != null)
        {
            secondaryLabel.text = cancel;
            secondary.onClick.AddListener(() => onCancel());
        }
        modal.gameObject.SetActive(true);
    }

    internal void HideMenu() => modal.gameObject.SetActive(false);

    private Button MenuButton(RectTransform parent, string name, Vector2 offset, out Text label)
    {
        RectTransform rect = Box(parent, name, Vector2.one * .5f, offset, new Vector2(246, 53));
        Image background = Fill(rect, GameUiTheme.Surface);
        background.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        label = Label(rect, "Label", 22, GameUiTheme.Paper, Vector2.zero, rect.sizeDelta);
        GameUiTheme.StyleButton(button);
        return button;
    }

    private Text Label(Transform parent, string name, int size, Color color, Vector2 offset, Vector2 dimensions)
    {
        Text label = Box(parent, name, Vector2.one * .5f, offset, dimensions).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return label;
    }

    private static Image Fill(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 dimensions)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = dimensions;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (font != null)
            Destroy(font);
    }
}
