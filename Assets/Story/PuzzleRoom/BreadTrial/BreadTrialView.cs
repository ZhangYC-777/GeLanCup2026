using System;
using UnityEngine;
using UnityEngine.UI;

// 小游戏 HUD 和菜单只覆盖当前场景，关闭后立即露出原来的解谜房。
internal sealed class BreadTrialView : MonoBehaviour
{
    private static readonly Color Cream = new Color32(248, 226, 190, 255);
    private static readonly Color Muted = new Color32(203, 185, 165, 255);
    private static readonly Color Brass = new Color32(220, 162, 97, 255);
    private static readonly Color Card = new Color32(29, 25, 31, 242);
    private static readonly Color Rust = new Color32(228, 126, 120, 255);

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
        Fill(upperLeft, Card);
        stamina = Label(upperLeft, "Stamina Text", 21, Cream, new Vector2(0, 10), new Vector2(206, 34));
        RectTransform bar = Box(upperLeft, "Stamina Track", Vector2.one * .5f, new Vector2(0, -18), new Vector2(182, 6));
        Fill(bar, new Color32(79, 58, 57, 255));
        staminaFill = Fill(Box(bar, "Fill", new Vector2(0f, .5f), Vector2.zero, bar.sizeDelta), Brass);
        staminaFill.rectTransform.pivot = new Vector2(0f, .5f);

        var upperCenter = Box(transform, "Objectives", new Vector2(.5f, 1f), new Vector2(0, -56), new Vector2(572, 70));
        Fill(upperCenter, Card);
        objective = Label(upperCenter, "Objective Text", 22, Cream, new Vector2(0, 12), new Vector2(550, 34));
        danger = Label(upperCenter, "Danger Text", 17, Muted, new Vector2(0, -16), new Vector2(550, 28));

        var upperRight = Box(transform, "Dash", new Vector2(.5f, 1f), new Vector2(445, -56), new Vector2(222, 70));
        Fill(upperRight, Card);
        dash = Label(upperRight, "Dash Text", 21, Cream, Vector2.zero, upperRight.sizeDelta);

        var footer = Box(transform, "Controls", new Vector2(.5f, 0f), new Vector2(0, 36), new Vector2(622, 38));
        Fill(footer, Card);
        Label(footer, "Key Hints", 19, Muted, Vector2.zero, footer.sizeDelta).text =
            "WASD / 方向键 移动     Shift / 空格 冲刺     Esc 暂停";

        modal = Box(transform, "Menu Backdrop", Vector2.one * .5f, Vector2.zero, Vector2.zero);
        Stretch(modal);
        Fill(modal, new Color32(8, 7, 12, 198)).raycastTarget = true;
        var frame = Box(modal, "Menu Frame", Vector2.one * .5f, Vector2.zero, new Vector2(650, 308));
        Fill(frame, new Color32(127, 92, 68, 255));
        var card = Box(frame, "Menu Card", Vector2.one * .5f, Vector2.zero, new Vector2(644, 302));
        Fill(card, new Color32(30, 25, 32, 255));
        Fill(Box(card, "Rule", Vector2.one * .5f, new Vector2(0, 110), new Vector2(56, 3)), Brass);
        heading = Label(card, "Title", 43, Cream, new Vector2(0, 62), new Vector2(610, 64));
        heading.fontStyle = FontStyle.Bold;
        message = Label(card, "Message", 23, Muted, new Vector2(0, -8), new Vector2(598, 64));
        primary = MenuButton(card, "Confirm", new Vector2(-133, -101), out primaryLabel);
        secondary = MenuButton(card, "Return", new Vector2(133, -101), out secondaryLabel);
    }

    internal void SetTexture(Texture texture) => backdrop.texture = texture;

    internal void UpdateHud(int health, int bread, float exitDistance, float shadowDistance, bool chasing, float cooldown)
    {
        stamina.text = $"体力  {health} / 100";
        staminaFill.rectTransform.localScale = new Vector3(health / 100f, 1f, 1f);
        staminaFill.color = health <= 40 ? Rust : Brass;
        objective.text = $"出口 {exitDistance:0.0} m       面包 {bread} / {BreadMazeLayout.BreadCount}";
        danger.text = chasing ? $"影子正在追你  ·  {shadowDistance:0.0} m" : $"影子距离  {shadowDistance:0.0} m";
        danger.color = chasing ? Rust : Muted;
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
        Image background = Fill(rect, new Color32(107, 74, 54, 255));
        background.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        label = Label(rect, "Label", 22, Cream, Vector2.zero, rect.sizeDelta);
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
