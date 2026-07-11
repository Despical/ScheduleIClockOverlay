using System;
using System.Globalization;
using Il2CppScheduleOne.GameTime;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ScheduleIClockOverlay.ClockMod), "Schedule I Clock Overlay", "1.0.1", "Despical")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleIClockOverlay;

public sealed class ClockMod : MelonMod
{
    private const float BoxWidth = 238f;
    private const float BoxHeight = 42f;
    private const float DayBoxHeight = 30f;
    private const float Margin = 18f;
    private const float BoxGap = 6f;

    private static readonly Color DefaultTextColor = Color.white;
    private static readonly Color CurfewTextColor = new(1f, 0.18f, 0.15f, 1f);

    private GUIStyle? labelStyle;
    private Texture2D? background;
    private ClockDisplay? lastClockDisplay;
    private string? lastDayText;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Schedule I Clock Overlay loaded.");
    }

    public override void OnUpdate()
    {
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
        if (lastClockDisplay == null)
        {
            return;
        }

        EnsureGui();
        labelStyle!.normal.textColor = lastClockDisplay.IsCurfew ? CurfewTextColor : DefaultTextColor;

        var x = Screen.width - BoxWidth - Margin;
        var clockRect = new Rect(x, Margin, BoxWidth, BoxHeight);
        GUI.DrawTexture(clockRect, background!, ScaleMode.StretchToFill);
        GUI.Label(clockRect, lastClockDisplay.Text, labelStyle);

        if (!string.IsNullOrWhiteSpace(lastDayText))
        {
            labelStyle.normal.textColor = DefaultTextColor;

            var dayRect = new Rect(x, Margin + BoxHeight + BoxGap, BoxWidth, DayBoxHeight);
            GUI.DrawTexture(dayRect, background!, ScaleMode.StretchToFill);
            GUI.Label(dayRect, lastDayText, labelStyle);
        }
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
}
