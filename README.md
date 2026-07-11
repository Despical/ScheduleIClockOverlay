# Schedule I Clock Overlay

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Game](https://img.shields.io/badge/Game-Schedule%20I-2ea44f)](https://store.steampowered.com/app/3164500/Schedule_I/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078d4?logo=windows)](https://github.com/Despical/ScheduleIClockOverlay/releases)

A small mod that shows the in-game clock in the top-right corner.
The overlay stays hidden while the game clock is unavailable, such as during loading or before entering gameplay.

## For Players

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) for Schedule I.
2. Start the game once, then close it.
3. Download `ScheduleIClockOverlay.dll` from the [releases page](https://github.com/Despical/ScheduleIClockOverlay/releases).
4. Copy the DLL into the game's `Mods` folder:

```text
Schedule I\Mods\ScheduleIClockOverlay.dll
```

Start the game again. The clock appears in the top-right corner once the in-game time is available.

## For Developers

Requirements:

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- MelonLoader installed in the game folder
- The game started once after installing MelonLoader so `MelonLoader\Il2CppAssemblies` exists

Build:

```powershell
dotnet publish .\src\ScheduleIClockOverlay\ScheduleIClockOverlay.csproj -c Release -o .\out /p:GameDir="C:\Path\To\Schedule I"
```

The built DLL will be:

```text
out\ScheduleIClockOverlay.dll
```

For local testing, copy it into:

```text
C:\Path\To\Schedule I\Mods\
```

## Notes

If **BepInEx** is also installed in the same game folder, remove it before using MelonLoader. Running both loaders together can cause startup conflicts.

## To remove the mod

Delete:

```text
Mods\ScheduleIClockOverlay.dll
```

## License
This code is under the [GPL-3.0 License](http://www.gnu.org/licenses/gpl-3.0.html).

See the [LICENSE](LICENSE) file for required notices and attributions.