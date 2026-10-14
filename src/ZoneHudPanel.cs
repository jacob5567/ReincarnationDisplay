using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReincarnationDisplay
{
    // Small panel parented to the HUD's key item (N'tormat) icon, shown while standing in a valid zone.
    internal sealed class ZoneHudPanel : MonoBehaviour
    {
        private const float PanelWidth = 260f;
        private const float FontSize = 15f;
        private const float FadeDuration = 0.15f;
        private const float DestinationRefreshInterval = 1f;
        private const float ScreenEdgeMargin = 4f;

        private static readonly Color BackgroundColor = new Color(0.07f, 0.03f, 0.1f, 0.85f);
        private const string HeaderColor = "#C38BFF";
        private const string NoteColor = "#A0A0A0";
        private const string WindowColor = "#E6D9FF";
        private const string WindowWarningColor = "#FFC24D";
        private const string WindowCriticalColor = "#FF5A5A";

        private RectTransform rectTransform;
        private RectTransform canvasRectTransform;
        private KeyItemIcon icon;
        private RectTransform iconRectTransform;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI label;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Vector3[] canvasCorners = new Vector3[4];

        private bool visible;
        private CursedBookSacrificeZone describedZone;
        private float destinationAge;
        private string outcome;
        private bool keepsStageCount;
        private int shownWindowTenths = int.MinValue;

        public static ZoneHudPanel Create(KeyItemIcon icon)
        {
            GameObject root = new GameObject("ReincarnationZonePanel", typeof(RectTransform));
            root.layer = icon.gameObject.layer;
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(icon.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);

            root.AddComponent<LayoutElement>().ignoreLayout = true;
            Image background = root.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = root.layer;
            textObject.transform.SetParent(rect, false);
            HGTextMeshProUGUI text = textObject.AddComponent<HGTextMeshProUGUI>();
            text.fontSize = FontSize;
            text.richText = true;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.raycastTarget = false;

            ZoneHudPanel panel = root.AddComponent<ZoneHudPanel>();
            panel.rectTransform = rect;
            panel.canvasGroup = group;
            panel.label = text;
            panel.icon = icon;
            panel.iconRectTransform = (RectTransform)icon.transform;
            Canvas canvas = icon.GetComponentInParent<Canvas>();
            panel.canvasRectTransform = canvas ? (RectTransform)canvas.rootCanvas.transform : null;
            return panel;
        }

        public void Hide()
        {
            visible = false;
        }

        public void Show(ZoneInfo zone)
        {
            visible = true;

            destinationAge += Time.unscaledDeltaTime;
            if (zone.Zone != describedZone || destinationAge >= DestinationRefreshInterval)
            {
                describedZone = zone.Zone;
                destinationAge = 0f;
                outcome = ZoneScanner.DescribeOutcome(zone.Zone, out keepsStageCount);
                shownWindowTenths = int.MinValue;
            }

            int windowTenths = zone.IsStageStart ? Mathf.Max(0, Mathf.CeilToInt(zone.WindowRemaining * 10f)) : -1;
            if (windowTenths != shownWindowTenths)
            {
                shownWindowTenths = windowTenths;
                label.text = BuildText(windowTenths);
            }
        }

        private string BuildText(int windowTenths)
        {
            string text = "<color=" + HeaderColor + "><b>REINCARNATION READY</b></color>\n<b>" + outcome + "</b>";
            if (keepsStageCount)
            {
                text += "\n<size=85%><color=" + NoteColor + ">Stage count unchanged</color></size>";
            }
            if (windowTenths >= 0)
            {
                float seconds = windowTenths / 10f;
                string color = seconds <= 5f ? WindowCriticalColor : seconds <= 10f ? WindowWarningColor : WindowColor;
                text += "\n<color=" + color + ">Window closes in " + seconds.ToString("0.0") + "s</color>";
            }
            return text;
        }

        private void LateUpdate()
        {
            PluginConfig settings = ReincarnationDisplayPlugin.Settings;
            rectTransform.localScale = Vector3.one * settings.PanelScale.Value;
            float target = visible ? 1f : 0f;
            if (visible || canvasGroup.alpha > 0f)
            {
                float clearance = GetContentTop() - iconRectTransform.rect.yMax;
                rectTransform.anchoredPosition = new Vector2(0f, clearance + settings.PanelOffsetY.Value);
                KeepOnScreen();
            }

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime / FadeDuration);
        }

        // Highest point, in the icon's local space, of anything drawn on or above the icon, such as the scrollable
        // page selector the N'tormat adds in extraDisplayHolder. The panel sits above it so nothing is covered.
        private float GetContentTop()
        {
            float top = Mathf.Max(iconRectTransform.rect.yMax, MeasureTop(iconRectTransform));
            Transform extraDisplay = icon ? icon.extraDisplayHolder : null;
            if (extraDisplay && !extraDisplay.IsChildOf(iconRectTransform))
            {
                top = Mathf.Max(top, MeasureTop(extraDisplay));
            }
            return top;
        }

        // Only visible graphics count, so empty layout containers don't push the panel up. A masked rect stands in
        // for its children, which are clipped to it (a scroll view's content can extend far past its viewport).
        private float MeasureTop(Transform node)
        {
            float top = float.NegativeInfinity;
            if (node == rectTransform || !node.gameObject.activeInHierarchy)
            {
                return top;
            }
            if (node is RectTransform rect)
            {
                bool masks = rect.GetComponent<RectMask2D>() || rect.GetComponent<Mask>();
                Graphic graphic = rect.GetComponent<Graphic>();
                if (masks || (graphic && graphic.enabled))
                {
                    rect.GetWorldCorners(corners);
                    top = Mathf.Max(iconRectTransform.InverseTransformPoint(corners[1]).y,
                        iconRectTransform.InverseTransformPoint(corners[2]).y);
                }
                if (masks)
                {
                    return top;
                }
            }
            for (int i = 0; i < node.childCount; i++)
            {
                top = Mathf.Max(top, MeasureTop(node.GetChild(i)));
            }
            return top;
        }

        // The icon sits near a screen edge, so slide the panel sideways if it would hang off the canvas.
        private void KeepOnScreen()
        {
            if (!canvasRectTransform || !(rectTransform.parent is RectTransform parent))
            {
                return;
            }
            rectTransform.GetWorldCorners(corners);
            canvasRectTransform.GetWorldCorners(canvasCorners);
            float margin = ScreenEdgeMargin * canvasRectTransform.lossyScale.x;
            float shift = 0f;
            if (corners[2].x > canvasCorners[2].x - margin)
            {
                shift = canvasCorners[2].x - margin - corners[2].x;
            }
            else if (corners[0].x < canvasCorners[0].x + margin)
            {
                shift = canvasCorners[0].x + margin - corners[0].x;
            }
            if (shift != 0f && parent.lossyScale.x > 0f)
            {
                rectTransform.anchoredPosition += new Vector2(shift / parent.lossyScale.x, 0f);
            }
        }
    }
}
