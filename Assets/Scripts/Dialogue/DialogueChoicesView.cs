using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 居中的 GAL 选项，每条各自成框。滚轮和滑块只负责浏览，点击只选中当前条目。
internal sealed class DialogueChoicesView
{
    private const float Width = 656f;
    private const float MinRowHeight = 62f;
    private const float Gap = 9f;
    private const float MaxListHeight = 390f;
    private const float FooterHeight = 55f;

    private sealed class Row
    {
        internal Image background;
        internal Image arrow;
        internal Text label;
        internal float top;
        internal float bottom;
    }

    private readonly List<Row> rows = new List<Row>();
    private readonly RectTransform overlay;
    private readonly RectTransform group;
    private readonly RectTransform content;
    private readonly ScrollRect scroll;
    private readonly Scrollbar scrollbar;
    private readonly Font font;
    private Action<int> onSelect;

    internal DialogueChoicesView(Canvas canvas, Font font)
    {
        this.font = font;
        if (!canvas.TryGetComponent(out GraphicRaycaster _))
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        if (EventSystem.current == null && UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("Dialogue Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(canvas.transform, false);
        }

        overlay = GameUiTheme.Box(canvas.transform, "Dialogue Choices");
        GameUiTheme.Place(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GameUiTheme.Fill(overlay, new Color32(9, 9, 9, 80), true);

        group = GameUiTheme.Box(overlay, "Options Group");
        group.anchorMin = group.anchorMax = new Vector2(.5f, .5f);
        group.anchoredPosition = Vector2.zero;

        var list = GameUiTheme.Box(group, "Option List");
        GameUiTheme.Place(list, Vector2.zero, Vector2.one, new Vector2(0f, FooterHeight), Vector2.zero);
        scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 44f;

        var viewport = GameUiTheme.Box(list, "Viewport");
        viewport.gameObject.AddComponent<RectMask2D>();
        GameUiTheme.Place(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GameUiTheme.Fill(viewport, Color.clear, true);
        content = GameUiTheme.Box(viewport, "Options");
        content.anchorMin = Vector2.up;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        scroll.viewport = viewport;
        scroll.content = content;

        var track = GameUiTheme.Box(list, "Scrollbar");
        GameUiTheme.Place(track, Vector2.right, Vector2.one, new Vector2(-14f, 2f), new Vector2(-2f, -2f));
        GameUiTheme.Art(track, GameUiTheme.SlotArt, true);
        scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var slidingArea = GameUiTheme.Box(track, "Sliding Area");
        GameUiTheme.Place(slidingArea, Vector2.zero, Vector2.one, Vector2.one * 2f, Vector2.one * -2f);
        var handle = GameUiTheme.Box(slidingArea, "Handle");
        GameUiTheme.Place(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scrollbar.targetGraphic = GameUiTheme.Art(handle, GameUiTheme.SlotArt, true);
        scrollbar.handleRect = handle;
        scroll.verticalScrollbar = scrollbar;

        var controls = GameUiTheme.Box(group, "Choice Controls");
        GameUiTheme.Place(controls, Vector2.zero, Vector2.right, new Vector2(0f, 1f), new Vector2(0f, 39f));
        var label = GameUiTheme.Box(controls, "Controls Label");
        GameUiTheme.Place(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GameUiTheme.TextShadow(GameUiTheme.Label(label, font, 24, GameUiTheme.Paper, TextAnchor.MiddleCenter, "↑ / ↓ 或鼠标选择     ·     F 确认"));
        Hide();
    }

    internal void Show(DialogueChoice[] options, Action<int> select)
    {
        onSelect = select;
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();
        float height = 2f;
        for (int i = 0; i < options.Length; i++)
        {
            if (i == rows.Count)
            {
                rows.Add(CreateRow(i));
            }

            Row row = rows[i];
            row.background.gameObject.SetActive(true);
            row.label.text = options[i].text;
            TextGenerationSettings settings = row.label.GetGenerationSettings(new Vector2(Width - 156f, 0f));
            float textHeight = row.label.cachedTextGeneratorForLayout.GetPreferredHeight(row.label.text, settings) / row.label.pixelsPerUnit;
            float rowHeight = Mathf.Max(MinRowHeight, Mathf.Ceil(textHeight + 22f));
            row.top = height;
            row.bottom = height + rowHeight;
            var rect = row.background.rectTransform;
            GameUiTheme.Place(rect, Vector2.up, Vector2.one, new Vector2(2f, -row.bottom), new Vector2(-2f, -row.top));
            height = row.bottom + Gap;
        }
        for (int i = options.Length; i < rows.Count; i++)
        {
            rows[i].background.gameObject.SetActive(false);
        }

        height -= Gap - 2f;
        float viewportHeight = Mathf.Min(MaxListHeight, height);
        content.sizeDelta = new Vector2(0f, height);
        group.sizeDelta = new Vector2(Width, viewportHeight + FooterHeight);
        bool overflow = height > viewportHeight;
        scrollbar.gameObject.SetActive(overflow);
        scroll.vertical = overflow;
        scroll.viewport.offsetMax = new Vector2(overflow ? -22f : 0f, 0f);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        Highlight(-1);
    }

    internal void Highlight(int index)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            bool focused = i == index;
            row.background.color = focused ? Color.white : GameUiTheme.Gray(152);
            row.arrow.enabled = focused;
            row.label.color = focused ? GameUiTheme.Paper : GameUiTheme.Silver;
        }

        if (index >= 0 && scroll.vertical)
        {
            float visible = scroll.viewport.rect.height;
            float maximum = content.rect.height - visible;
            if (maximum > 0f)
            {
                float offset = Mathf.Clamp(content.anchoredPosition.y, 0f, maximum);
                var row = rows[index];
                if (row.top < offset || row.bottom - row.top >= visible)
                {
                    offset = row.top - 2f;
                }
                else if (row.bottom > offset + visible)
                {
                    offset = row.bottom - visible + 2f;
                }
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / maximum);
            }
        }
    }

    internal void Hide()
    {
        onSelect = null;
        overlay.gameObject.SetActive(false);
    }

    private Row CreateRow(int index)
    {
        var rect = GameUiTheme.Box(content, "Choice");
        Image background = GameUiTheme.Art(rect, GameUiTheme.ButtonArt, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(.84f, .84f, .84f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = GameUiTheme.Gray(165);
        colors.fadeDuration = .08f;
        button.colors = colors;
        button.onClick.AddListener(() => onSelect?.Invoke(index));

        var marker = GameUiTheme.Box(rect, "Arrow");
        GameUiTheme.Place(marker, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(45f, -12f), new Vector2(83f, 12f));
        Image arrow = GameUiTheme.Art(marker, GameUiTheme.ArrowArt);
        arrow.preserveAspect = true;

        var textRect = GameUiTheme.Box(rect, "Label");
        GameUiTheme.Place(textRect, Vector2.zero, Vector2.one, new Vector2(77f, 7f), new Vector2(-77f, -7f));
        Text label = GameUiTheme.Label(textRect, font, 31, GameUiTheme.Paper, TextAnchor.MiddleCenter);
        return new Row { background = background, arrow = arrow, label = label };
    }
}
