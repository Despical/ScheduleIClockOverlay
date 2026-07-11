using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ScheduleIClockOverlay.ClockMod), "Schedule I Clock Overlay", "1.0.0", "Despical")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleIClockOverlay;

public sealed class ClockMod : MelonMod
{
    private const float BoxWidth = 238f;
    private const float BoxHeight = 42f;
    private const float Margin = 18f;
    private static readonly Color DefaultTextColor = Color.white;
    private static readonly Color CurfewTextColor = new(1f, 0.18f, 0.15f, 1f);

    private GUIStyle? labelStyle;
    private Texture2D? background;
    private object? timeManager;
    private Type? timeManagerType;
    private float nextLookupAt;
    private ClockDisplay? lastClockDisplay;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Schedule I Clock Overlay loaded.");
    }

    public override void OnUpdate()
    {
        if (UnityEngine.Time.unscaledTime >= nextLookupAt)
        {
            nextLookupAt = UnityEngine.Time.unscaledTime + 1f;
            timeManagerType ??= ResolveTimeManagerType();
            timeManager = FindTimeManager();
        }

        lastClockDisplay = ReadClockDisplay(timeManager);
    }

    public override void OnGUI()
    {
        if (lastClockDisplay == null)
        {
            return;
        }

        EnsureGui();
        labelStyle!.normal.textColor = lastClockDisplay.IsCurfew ? CurfewTextColor : DefaultTextColor;

        var x = Screen.width - BoxWidth - Margin;
        var rect = new Rect(x, Margin, BoxWidth, BoxHeight);
        GUI.DrawTexture(rect, background!, ScaleMode.StretchToFill);
        GUI.Label(rect, lastClockDisplay.Text, labelStyle);
    }

    private void EnsureGui()
    {
        if (labelStyle != null && background != null)
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

        background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.58f));
        background.Apply();
    }

    private object? FindTimeManager()
    {
        try
        {
            var direct = FindObjectOfType(timeManagerType);
            if (direct != null)
            {
                return direct;
            }

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                var typeName = behaviour.GetIl2CppType()?.FullName;
                if (typeName == "ScheduleOne.GameTime.TimeManager")
                {
                    return behaviour;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"TimeManager lookup failed: {ex.Message}");
        }

        return null;
    }

    private static Type? ResolveTimeManagerType()
    {
        foreach (var typeName in new[] { "Il2CppScheduleOne.GameTime.TimeManager", "ScheduleOne.GameTime.TimeManager" })
        {
            var type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(found => found != null);

            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static object? FindObjectOfType(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        var method = typeof(UnityEngine.Object)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(candidate => candidate.Name == nameof(UnityEngine.Object.FindObjectOfType) && candidate.IsGenericMethodDefinition)
            .FirstOrDefault(candidate => candidate.GetParameters().Length == 0);

        return method?.MakeGenericMethod(type).Invoke(null, null);
    }

    private ClockDisplay? ReadClockDisplay(object? manager)
    {
        if (manager == null)
        {
            return null;
        }

        var type = manager.GetType();

        foreach (var member in new[] { "FormattedCurrentTime", "CurrentTimeString", "TimeString", "Get12HourTime", "GetCurrentTimeString" })
        {
            var value = ReadMember(manager, type, member);
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return CreateClockDisplay(text.Trim());
            }
        }

        var currentTime = ReadMember(manager, type, "CurrentTime") ?? ReadMember(manager, type, "currentTime");
        var formatted = CreateClockDisplay(currentTime);
        if (formatted != null)
        {
            return formatted;
        }

        var hour = ToInt(ReadMember(manager, type, "Hour") ?? ReadMember(manager, type, "CurrentHour") ?? ReadMember(manager, type, "hour"));
        var minute = ToInt(ReadMember(manager, type, "Minute") ?? ReadMember(manager, type, "CurrentMinute") ?? ReadMember(manager, type, "minute"));
        if (hour.HasValue && minute.HasValue)
        {
            return CreateClockDisplay(Mod(hour.Value, 24), Mod(minute.Value, 60));
        }

        return null;
    }

    private object? ReadMember(object target, Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            var property = type.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(target);
            }

            var field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (method != null)
            {
                return method.Invoke(target, null);
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Could not read {name}: {ex.Message}");
        }

        return null;
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

    private static ClockDisplay? CreateClockDisplay(string text)
    {
        if (TryParseClockText(text, out var hour, out var minute))
        {
            return CreateClockDisplay(hour, minute);
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

    private static bool TryParseClockText(string text, out int hour, out int minute)
    {
        foreach (var format in new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" })
        {
            if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                hour = parsed.Hour;
                minute = parsed.Minute;
                return true;
            }
        }

        hour = 0;
        minute = 0;
        return false;
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

    private static int? ToInt(object? value)
    {
        var number = ToDouble(value);
        return number.HasValue ? (int)Math.Round(number.Value) : null;
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
}
