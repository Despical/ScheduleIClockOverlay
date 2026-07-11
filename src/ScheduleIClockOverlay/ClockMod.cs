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
    private const float BoxWidth = 154f;
    private const float BoxHeight = 42f;
    private const float Margin = 18f;

    private GUIStyle? labelStyle;
    private Texture2D? background;
    private object? timeManager;
    private Type? timeManagerType;
    private float nextLookupAt;
    private string? lastClockText;

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

        lastClockText = ReadClockText(timeManager);
    }

    public override void OnGUI()
    {
        if (string.IsNullOrWhiteSpace(lastClockText))
        {
            return;
        }

        EnsureGui();

        var x = Screen.width - BoxWidth - Margin;
        var rect = new Rect(x, Margin, BoxWidth, BoxHeight);
        GUI.DrawTexture(rect, background!, ScaleMode.StretchToFill);
        GUI.Label(rect, lastClockText, labelStyle);
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
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
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

    private string? ReadClockText(object? manager)
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
                return text.Trim();
            }
        }

        var currentTime = ReadMember(manager, type, "CurrentTime") ?? ReadMember(manager, type, "currentTime");
        var formatted = FormatCurrentTime(currentTime);
        if (formatted != null)
        {
            return formatted;
        }

        var hour = ToInt(ReadMember(manager, type, "Hour") ?? ReadMember(manager, type, "CurrentHour") ?? ReadMember(manager, type, "hour"));
        var minute = ToInt(ReadMember(manager, type, "Minute") ?? ReadMember(manager, type, "CurrentMinute") ?? ReadMember(manager, type, "minute"));
        if (hour.HasValue && minute.HasValue)
        {
            return $"{Mod(hour.Value, 24):00}:{Mod(minute.Value, 60):00}";
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

    private static string? FormatCurrentTime(object? value)
    {
        var number = ToDouble(value);
        if (!number.HasValue)
        {
            return null;
        }

        var raw = number.Value;
        if (raw >= 0 && raw <= 2359 && Math.Abs(raw % 1) < 0.001)
        {
            var time = (int)raw;
            return $"{Mod(time / 100, 24):00}:{Mod(time % 100, 60):00}";
        }

        if (raw >= 0 && raw < 24)
        {
            var hour = (int)Math.Floor(raw);
            var minute = (int)Math.Round((raw - hour) * 60);
            return $"{Mod(hour, 24):00}:{Mod(minute, 60):00}";
        }

        if (raw >= 0 && raw < 1440)
        {
            var totalMinutes = (int)Math.Round(raw);
            return $"{Mod(totalMinutes / 60, 24):00}:{Mod(totalMinutes, 60):00}";
        }

        return null;
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
}
