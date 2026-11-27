using UnityEngine;
using UnityEngine.UI;

// 统一使用现成像素边框。大面板九宫格缩放，横向铭牌保留两端雕花。
internal static class GameUiTheme
{
    private const string ArtPath = "GothicUI/";

    internal static readonly Color Ink = Gray(18);
    internal static readonly Color Surface = Gray(36);
    internal static readonly Color Silver = Gray(169);
    internal static readonly Color Paper = Gray(213);
    internal static readonly Color Muted = Gray(153);
    internal static readonly Color Danger = new Color32(170, 143, 150, 255);
    internal static readonly Color Vital = Gray(141);
    internal static readonly Color Shade = new Color32(9, 9, 9, 176);

    internal static readonly Sprite FrameArt = Resources.Load<Sprite>(ArtPath + "Frame");
    internal static readonly Sprite ButtonArt = Resources.Load<Sprite>(ArtPath + "Button");
    internal static readonly Sprite ArrowArt = Resources.Load<Sprite>(ArtPath + "Arrow");
    internal static readonly Sprite SlotArt = Resources.Load<Sprite>(ArtPath + "Slot");
    internal static readonly Sprite MeterArt = Resources.Load<Sprite>(ArtPath + "Meter");

    internal static Color Gray(byte tone)
    {
        return new Color32(tone, tone, tone, 255);
    }

    internal static RectTransform Box(Transform parent, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 bottomLeft, Vector2 topRight)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = bottomLeft;
        rect.offsetMax = topRight;
    }

    internal static Image Fill(RectTransform rect, Color color, bool raycast = false)
    {
        if (!rect.TryGetComponent(out Image image))
        {
            image = rect.gameObject.AddComponent<Image>();
        }
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    internal static Image Art(RectTransform rect, Sprite sprite, bool raycast = false)
    {
        Image image = Fill(rect, Color.white, raycast);
        ApplyArt(image, sprite);
        return image;
    }

    private static void ApplyArt(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.overrideSprite = null;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        if (sprite != null && sprite == FrameArt)
        {
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = .62f;
        }
        else
        {
            image.pixelsPerUnitMultiplier = 1f;
        }
    }

    internal static void Frame(RectTransform rect)
    {
        Art(rect, FrameArt);
    }

    internal static RectTransform MeterTrack(RectTransform rect)
    {
        var well = Box(rect, "Meter track");
        Place(well, Vector2.zero, Vector2.one, new Vector2(17f, 5f), new Vector2(-17f, -5f));
        Fill(well, Ink);
        var rim = Box(rect, "Meter art");
        Place(rim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Art(rim, MeterArt);
        return well;
    }

    internal static void TextShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(4, 4, 4, 220);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    internal static Text Label(RectTransform rect, Font font, int size, Color color, TextAnchor alignment, string value = "")
    {
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    internal static void StyleButton(Button button)
    {
        if (!(button.targetGraphic is Image image))
        {
            return;
        }

        ApplyArt(image, ButtonArt);
        image.color = Color.white;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Gray(171);
        colors.highlightedColor = Gray(231);
        colors.selectedColor = Color.white;
        colors.pressedColor = Gray(130);
        colors.disabledColor = Gray(89);
        colors.fadeDuration = .08f;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = Paper;
            label.alignment = TextAnchor.MiddleCenter;
        }
    }
}
