# ReincarnationDisplay

A client-side Risk of Rain 2 mod for the Hallowed Concepts DLC. It shows where the **Reincarnation** N'tormat page actually works.

While you hold an unused Reincarnation page:

- **World rings** mark the edge of every sacrifice zone that is currently valid, such as the teleporter's ritual zone, drawn at the zone's real radius and following the terrain.
- **A panel above the N'tormat icon** appears only while you stand inside a valid zone. It shows:
  - where a sacrifice would send the team (a specific stage, the possible stages for a random reroll, or the Veil of Memory)
  - whether the stage count stays the same
  - a countdown for the 30-second stage-start reroll window

The mod never changes gameplay and never sends anything over the network. It works when you join a host who doesn't have it installed.

## Configuration

Edit `BepInEx/config/sevenstack.ReincarnationDisplay.cfg`, or use the in-game settings menu if [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed.

| Setting | Default | Description |
|---|---|---|
| `ShowPanel` | true | Show the in-zone panel. |
| `PanelOffsetY` / `PanelScale` | 8 / 1 | Panel position and size. |
| `ShowWorldRings` | true | Draw zone rings in the world. |
| `RingMaxDistance` | 300 | Hide rings further than this, in meters. |
| `StageStartWindowSeconds` | 30 | Length of the stage-start reroll window. |
| `HideStageStartZoneAfterWindow` | true | Stop showing the stage-start zone once the window ends. |
| `DebugLogging` / `DumpZonesKey` | false / F8 | Log zone details, for troubleshooting. |

## Notes

- On a client, a zone that leads to "the next stage" can only say *Next stage (stage N)*. The game doesn't send the chosen stage to clients; the host sees the stage name.
- Random destinations (the stage-start reroll, scene collections) list every possible stage. The game picks one only when the ritual fires.

## Building

Requires the .NET 8 SDK and a local Risk of Rain 2 install.

```sh
dotnet build -c Release   # also copies the DLL into the r2modman Default profile
./build.sh                # builds and writes dist/ReincarnationDisplay-<version>.zip for Thunderstore
```

Paths default to the Steam and r2modman locations on Linux. Override `RoR2Dir` and `ProfileDir` in a `Directory.Build.props.user` file (see `Directory.Build.props`).
