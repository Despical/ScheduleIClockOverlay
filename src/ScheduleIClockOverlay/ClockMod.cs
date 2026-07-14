using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Il2CppScheduleOne.GameTime;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(ScheduleIClockOverlay.ClockMod), "ClockOverlay", ScheduleIClockOverlay.ClockMod.ModVersion, "Despical")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleIClockOverlay;

public sealed class ClockMod : MelonMod
{
    public const string ModVersion = "1.0.4";

    private const float BoxWidth = 238f;
    private const float BoxHeight = 42f;
    private const float DayBoxHeight = BoxHeight;
    private const float Margin = 18f;
    private const float BoxGap = 6f;
    private const float MenuWidth = 340f;
    private const float MenuHeight = 342f;
    private const float DefaultBackgroundOpacity = 0.58f;
    private const string ConfigFileName = "ClockOverlay.json";
    private const string LegacyConfigFileName = "ScheduleIClockOverlay.json";

    private static readonly Color DefaultTextColor = Color.white;
    private static readonly Color CurfewTextColor = new(1f, 0.18f, 0.15f, 1f);
    private static readonly Color EditTextColor = new(1f, 0.88f, 0.22f, 1f);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private GUIStyle? labelStyle;
    private GUIStyle? menuLabelStyle;
    private GUIStyle? headerStyle;
    private GUIStyle? sliderStyle;
    private GUIStyle? sliderThumbStyle;
    private Texture2D? background;
    private Texture2D? editBackground;
    private Texture2D? menuBackground;
    private Texture2D? sliderBackground;
    private Texture2D? sliderThumb;
    private ClockDisplay? lastClockDisplay;
    private string? lastDayText;
    private OverlayConfig config = new();
    private Rect menuRect;
    private DragTarget draggingTarget = DragTarget.None;
    private Vector2 dragOffset;
    private bool editMode;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private string configPath = string.Empty;
    private bool draggingMenu;
    private Vector2 menuDragOffset;

    public override void OnInitializeMelon()
    {
        config = LoadConfig();
        ResetMenuPosition();
        LoggerInstance.Msg($"ClockOverlay v{ModVersion} loaded.");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleEditMode();
        }

        if (!TimeManager.InstanceExists)
        {
            lastClockDisplay = null;
            lastDayText = null;
            return;
        }

        var timeManager = TimeManager.Instance;
        lastClockDisplay = CreateClockDisplay(timeManager.CurrentTime);
        lastDayText = $"Day {(timeManager.ElapsedDays + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    public override void OnGUI()
    {
        if (lastClockDisplay == null && !editMode)
        {
            return;
        }

        EnsureGui();
        var clockRect = GetClockRect();
        var dayRect = GetDayRect();

        if (editMode)
        {
            if (config.Clock.Enabled)
            {
                HandleDrag(clockRect, DragTarget.Clock);
            }

            if (config.DayCounter.Enabled)
            {
                HandleDrag(dayRect, DragTarget.DayCounter);
            }

            clockRect = GetClockRect();
            dayRect = GetDayRect();
        }

        if (config.Clock.Enabled)
        {
            var clockText = lastClockDisplay?.Text ?? "Clock";
            labelStyle!.normal.textColor = editMode ? EditTextColor : lastClockDisplay!.IsCurfew ? CurfewTextColor : DefaultTextColor;
            DrawBackground(clockRect, config.Clock.BackgroundOpacity);
            GUI.Label(clockRect, clockText, labelStyle);
        }

        if (config.DayCounter.Enabled)
        {
            var dayText = string.IsNullOrWhiteSpace(lastDayText) ? "Day Counter" : lastDayText;
            labelStyle!.normal.textColor = editMode ? EditTextColor : DefaultTextColor;
            DrawBackground(dayRect, config.DayCounter.BackgroundOpacity);
            GUI.Label(dayRect, dayText, labelStyle);
        }

        if (editMode)
        {
            HandleMenuDrag();
            DrawEditMenu();
            ClampMenuToScreen();
        }
    }

    private void EnsureGui()
    {
        if (labelStyle != null && background != null && editBackground != null && menuBackground != null &&
            sliderStyle != null && sliderThumbStyle != null && sliderBackground != null && sliderThumb != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = DefaultTextColor }
        };

        menuLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            normal = { textColor = DefaultTextColor }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = DefaultTextColor }
        };

        background = new Texture2D(1, 1);
        background.SetPixel(0, 0, Color.black);
        background.Apply();

        editBackground = new Texture2D(1, 1);
        editBackground.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.02f, 1f));
        editBackground.Apply();

        menuBackground = new Texture2D(1, 1);
        menuBackground.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.02f, 0.9f));
        menuBackground.Apply();

        sliderBackground = new Texture2D(1, 1);
        sliderBackground.SetPixel(0, 0, new Color(0.38f, 0.38f, 0.38f, 1f));
        sliderBackground.Apply();

        sliderThumb = new Texture2D(1, 1);
        sliderThumb.SetPixel(0, 0, new Color(0.78f, 0.78f, 0.78f, 1f));
        sliderThumb.Apply();

        sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 8f,
            normal = { background = null },
            hover = { background = null },
            active = { background = null }
        };

        sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth = 14f,
            fixedHeight = 18f,
            normal = { background = sliderThumb },
            hover = { background = sliderThumb },
            active = { background = sliderThumb }
        };
    }

    private void DrawBackground(Rect rect, float opacity)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var previousColor = GUI.color;
        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * opacity);
        GUI.DrawTexture(rect, editMode ? editBackground! : background!, ScaleMode.StretchToFill);
        GUI.color = previousColor;
    }

    private OverlayConfig LoadConfig()
    {
        configPath = Path.Combine(MelonEnvironment.ModsDirectory, ConfigFileName);
        try
        {
            Directory.CreateDirectory(MelonEnvironment.ModsDirectory);
            var legacyConfigPath = Path.Combine(MelonEnvironment.ModsDirectory, LegacyConfigFileName);
            if (!File.Exists(configPath) && File.Exists(legacyConfigPath))
            {
                File.Copy(legacyConfigPath, configPath);
            }

            if (!File.Exists(configPath))
            {
                var defaults = CreateDefaultConfig();
                SaveConfig(defaults);
                return defaults;
            }

            var loaded = JsonSerializer.Deserialize<OverlayConfig>(File.ReadAllText(configPath), JsonOptions) ?? CreateDefaultConfig();
            loaded.Normalize();
            return loaded;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Could not load config from {configPath}: {ex.Message}");
            return CreateDefaultConfig();
        }
    }

    private void SaveConfig()
    {
        SaveConfig(config);
    }

    private void SaveConfig(OverlayConfig value)
    {
        try
        {
            Directory.CreateDirectory(MelonEnvironment.ModsDirectory);
            File.WriteAllText(configPath, JsonSerializer.Serialize(value, JsonOptions));
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Could not save config to {configPath}: {ex.Message}");
        }
    }

    private static OverlayConfig CreateDefaultConfig()
    {
        var value = new OverlayConfig();
        value.Normalize();
        return value;
    }

    private Rect GetClockRect()
    {
        config.Clock.Position.Clamp(BoxWidth, BoxHeight);
        return new Rect(config.Clock.Position.X, config.Clock.Position.Y, BoxWidth, BoxHeight);
    }

    private Rect GetDayRect()
    {
        config.DayCounter.Position.Clamp(BoxWidth, DayBoxHeight);
        return new Rect(config.DayCounter.Position.X, config.DayCounter.Position.Y, BoxWidth, DayBoxHeight);
    }

    private void DrawEditMenu()
    {
        GUI.DrawTexture(menuRect, menuBackground!, ScaleMode.StretchToFill);
        GUI.Box(menuRect, $"Clock Overlay v{ModVersion}");
        var x = menuRect.x;
        var y = menuRect.y;

        GUI.Label(new Rect(x + 16f, y + 28f, MenuWidth - 32f, 22f), "Press G to close - drag boxes with the mouse", headerStyle);

        GUI.Label(new Rect(x + 22f, y + 66f, 94f, 24f), "Clock:", menuLabelStyle);
        var clockEnabled = GUI.Toggle(new Rect(x + 120f, y + 66f, 100f, 24f), config.Clock.Enabled, config.Clock.Enabled ? "  On" : "  Off");
        if (clockEnabled != config.Clock.Enabled)
        {
            config.Clock.Enabled = clockEnabled;
            SaveConfig();
        }

        DrawOpacitySlider(new Rect(x + 22f, y + 96f, MenuWidth - 44f, 24f), "Clock box:", config.Clock);

        GUI.Label(new Rect(x + 22f, y + 126f, 94f, 24f), "Day Counter:", menuLabelStyle);
        var dayEnabled = GUI.Toggle(new Rect(x + 120f, y + 126f, 100f, 24f), config.DayCounter.Enabled, config.DayCounter.Enabled ? "  On" : "  Off");
        if (dayEnabled != config.DayCounter.Enabled)
        {
            config.DayCounter.Enabled = dayEnabled;
            SaveConfig();
        }

        DrawOpacitySlider(new Rect(x + 22f, y + 156f, MenuWidth - 44f, 24f), "Day box:", config.DayCounter);

        if (GUI.Button(new Rect(x + 22f, y + 192f, MenuWidth - 44f, 30f), "Reset positions"))
        {
            ApplyPreset(Corner.TopRight);
        }

        GUI.Label(new Rect(x + 22f, y + 236f, MenuWidth - 44f, 20f), "Preset", menuLabelStyle);
        if (GUI.Button(new Rect(x + 22f, y + 262f, 136f, 30f), "Top Left"))
        {
            ApplyPreset(Corner.TopLeft);
        }

        if (GUI.Button(new Rect(x + 182f, y + 262f, 136f, 30f), "Top Right"))
        {
            ApplyPreset(Corner.TopRight);
        }

        if (GUI.Button(new Rect(x + 22f, y + 298f, 136f, 30f), "Bottom Left"))
        {
            ApplyPreset(Corner.BottomLeft);
        }

        if (GUI.Button(new Rect(x + 182f, y + 298f, 136f, 30f), "Bottom Right"))
        {
            ApplyPreset(Corner.BottomRight);
        }
    }

    private void DrawOpacitySlider(Rect rect, string label, OverlayElement element)
    {
        GUI.Label(new Rect(rect.x, rect.y, 100f, rect.height), label, menuLabelStyle);
        GUI.DrawTexture(new Rect(rect.x + 100f, rect.y + 12f, 140f, 4f), sliderBackground!, ScaleMode.StretchToFill);
        var opacity = GUI.HorizontalSlider(
            new Rect(rect.x + 100f, rect.y + 5f, 140f, 18f),
            element.BackgroundOpacity,
            0f,
            1f,
            sliderStyle!,
            sliderThumbStyle!);
        opacity = Mathf.Round(opacity * 100f) / 100f;
        GUI.Label(new Rect(rect.x + 248f, rect.y, 48f, rect.height), $"{Mathf.RoundToInt(opacity * 100f)}%", menuLabelStyle);

        if (!Mathf.Approximately(opacity, element.BackgroundOpacity))
        {
            element.BackgroundOpacity = opacity;
            SaveConfig();
        }
    }

    private void HandleMenuDrag()
    {
        var currentEvent = Event.current;
        if (currentEvent == null)
        {
            return;
        }

        var titleRect = new Rect(menuRect.x, menuRect.y, menuRect.width, 24f);
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && titleRect.Contains(currentEvent.mousePosition))
        {
            draggingMenu = true;
            menuDragOffset = currentEvent.mousePosition - new Vector2(menuRect.x, menuRect.y);
            currentEvent.Use();
            return;
        }

        if (!draggingMenu)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDrag)
        {
            var position = currentEvent.mousePosition - menuDragOffset;
            menuRect.x = position.x;
            menuRect.y = position.y;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp)
        {
            draggingMenu = false;
            currentEvent.Use();
        }
    }

    private void HandleDrag(Rect rect, DragTarget target)
    {
        var currentEvent = Event.current;
        if (currentEvent == null)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
        {
            draggingTarget = target;
            dragOffset = currentEvent.mousePosition - new Vector2(rect.x, rect.y);
            currentEvent.Use();
            return;
        }

        if (draggingTarget != target)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDrag)
        {
            var position = currentEvent.mousePosition - dragOffset;
            GetPosition(target).Set(position.x, position.y, rect.width, rect.height);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp)
        {
            draggingTarget = DragTarget.None;
            SaveConfig();
            currentEvent.Use();
        }
    }

    private OverlayPosition GetPosition(DragTarget target)
    {
        return target == DragTarget.Clock ? config.Clock.Position : config.DayCounter.Position;
    }

    private void ApplyPreset(Corner corner)
    {
        var x = corner is Corner.TopLeft or Corner.BottomLeft ? Margin : Screen.width - BoxWidth - Margin;
        var topY = corner is Corner.TopLeft or Corner.TopRight
            ? Margin
            : Screen.height - BoxHeight - BoxGap - DayBoxHeight - Margin;

        config.Clock.Position.Set(x, topY, BoxWidth, BoxHeight);
        config.DayCounter.Position.Set(x, topY + BoxHeight + BoxGap, BoxWidth, DayBoxHeight);
        SaveConfig();
    }

    private void ToggleEditMode()
    {
        editMode = !editMode;
        draggingTarget = DragTarget.None;

        if (editMode)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ResetMenuPosition();
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockState;
        SaveConfig();
    }

    private void ResetMenuPosition()
    {
        menuRect = new Rect(
            Mathf.Max(Margin, (Screen.width - MenuWidth) / 2f),
            Mathf.Max(Margin, (Screen.height - MenuHeight) / 2f),
            MenuWidth,
            MenuHeight);
    }

    private void ClampMenuToScreen()
    {
        menuRect.x = Mathf.Clamp(menuRect.x, 0f, Mathf.Max(0f, Screen.width - menuRect.width));
        menuRect.y = Mathf.Clamp(menuRect.y, 0f, Mathf.Max(0f, Screen.height - menuRect.height));
    }

    private static ClockDisplay? CreateClockDisplay(object? value)
    {
        var number = ToDouble(value);
        if (!number.HasValue)
        {
            return null;
        }

        var raw = number.Value;
        if (raw >= 0 && raw <= 2400 && Math.Abs(raw % 1) < 0.001)
        {
            var time = (int)raw;
            return CreateClockDisplay(Mod(time / 100, 24), Mod(time % 100, 60));
        }

        if (raw >= 0 && raw < 24)
        {
            var hour = (int)Math.Floor(raw);
            var minute = (int)Math.Round((raw - hour) * 60);
            return CreateClockDisplay(Mod(hour, 24), Mod(minute, 60));
        }

        if (raw >= 0 && raw < 1440)
        {
            var totalMinutes = (int)Math.Round(raw);
            return CreateClockDisplay(Mod(totalMinutes / 60, 24), Mod(totalMinutes, 60));
        }

        return null;
    }

    private static ClockDisplay CreateClockDisplay(int hour, int minute)
    {
        hour = Mod(hour, 24);
        minute = Mod(minute, 60);
        var totalMinutes = hour * 60 + minute;
        var period = GetDayPeriod(totalMinutes);
        var curfew = IsCurfew(totalMinutes);
        return new ClockDisplay($"{Format12Hour(hour, minute)} ({period})", curfew);
    }

    private static string Format12Hour(int hour, int minute)
    {
        var suffix = hour < 12 ? "AM" : "PM";
        var displayHour = hour % 12;
        if (displayHour == 0)
        {
            displayHour = 12;
        }

        return $"{displayHour}:{minute:00} {suffix}";
    }

    private static string GetDayPeriod(int totalMinutes)
    {
        if (totalMinutes >= 6 * 60 && totalMinutes < 12 * 60)
        {
            return "Morning";
        }

        if (totalMinutes >= 12 * 60 && totalMinutes < 18 * 60)
        {
            return "Afternoon";
        }

        if (totalMinutes >= 18 * 60)
        {
            return "Night";
        }

        return "Late Night";
    }

    private static bool IsCurfew(int totalMinutes)
    {
        return totalMinutes >= 21 * 60 || totalMinutes < 5 * 60;
    }

    private static double? ToDouble(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static int Mod(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private sealed record ClockDisplay(string Text, bool IsCurfew);

    private enum DragTarget
    {
        None,
        Clock,
        DayCounter
    }

    private enum Corner
    {
        TopLeft,
        BottomLeft,
        TopRight,
        BottomRight
    }

    private sealed class OverlayConfig
    {
        public OverlayElement Clock { get; set; } = new(new OverlayPosition());
        public OverlayElement DayCounter { get; set; } = new(new OverlayPosition());

        public void Normalize()
        {
            Clock ??= new OverlayElement(new OverlayPosition());
            DayCounter ??= new OverlayElement(new OverlayPosition());
            Clock.Position ??= new OverlayPosition();
            DayCounter.Position ??= new OverlayPosition();
            Clock.BackgroundOpacity = NormalizeOpacity(Clock.BackgroundOpacity);
            DayCounter.BackgroundOpacity = NormalizeOpacity(DayCounter.BackgroundOpacity);

            if (!Clock.Position.IsConfigured)
            {
                Clock.Position.X = Screen.width - BoxWidth - Margin;
                Clock.Position.Y = Margin;
            }

            if (!DayCounter.Position.IsConfigured)
            {
                DayCounter.Position.X = Clock.Position.X;
                DayCounter.Position.Y = Clock.Position.Y + BoxHeight + BoxGap;
            }

            Clock.Position.Clamp(BoxWidth, BoxHeight);
            DayCounter.Position.Clamp(BoxWidth, DayBoxHeight);
        }

        private static float NormalizeOpacity(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultBackgroundOpacity
                : Mathf.Clamp01(value);
        }
    }

    private sealed class OverlayElement
    {
        public OverlayElement()
        {
        }

        public OverlayElement(OverlayPosition position)
        {
            Position = position;
        }

        public bool Enabled { get; set; } = true;
        public float BackgroundOpacity { get; set; } = DefaultBackgroundOpacity;
        public OverlayPosition Position { get; set; } = new();
    }

    private sealed class OverlayPosition
    {
        public float X { get; set; } = float.NaN;
        public float Y { get; set; } = float.NaN;

        [JsonIgnore]
        public bool IsConfigured => !float.IsNaN(X) && !float.IsNaN(Y);

        public void Set(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Clamp(width, height);
        }

        public void Clamp(float width, float height)
        {
            if (float.IsNaN(X))
            {
                X = Screen.width - width - Margin;
            }

            if (float.IsNaN(Y))
            {
                Y = Margin;
            }

            X = Mathf.Clamp(X, 0f, Mathf.Max(0f, Screen.width - width));
            Y = Mathf.Clamp(Y, 0f, Mathf.Max(0f, Screen.height - height));
        }
    }
}
