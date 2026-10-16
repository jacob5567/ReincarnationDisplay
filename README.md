# ReincarnationDisplay

A client-side Risk of Rain 2 mod for the Hallowed Concepts DLC. It shows where the **Reincarnation** N'tormat page actually works.

While you hold an unused Reincarnation page (including one that is currently disabled):

- **World rings** mark the edge of every sacrifice zone that is currently valid, such as the teleporter's ritual zone, drawn at the zone's real size along the ground. Stretches where the outline would jump onto a rock or hang over a drop are left out.
- **A small hint above the N'tormat icon**, "Sacrifice zone on this stage", appears while the stage has a valid zone and you aren't standing in one. The stage-start reroll zone doesn't count.
- **A panel above the N'tormat icon** appears only while you stand inside a valid zone. It shows:
  - what a sacrifice would do: send the team to a named stage, continue to the next stage, reroll the current stage, enter a random Winnowing trial, or open the Veil of Memory
  - whether the stage count stays the same
  - a countdown for the 30-second stage-start reroll window
  - a red **REINCARNATION DISABLED** warning when your page is disabled, for example by Hex of Loss

The mod never changes gameplay and never sends anything over the network. It works when you join a host who doesn't have it installed.

## Configuration

Edit `BepInEx/config/sevenstack.ReincarnationDisplay.cfg`, or use the in-game settings menu if [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed.

| Setting | Default | Description |
|---|---|---|
| `ShowPanel` | true | Show the in-zone panel. |
| `ShowZoneIndicator` | true | Show the "Sacrifice zone on this stage" hint. |
| `PanelOffsetY` / `PanelScale` | -6 / 1 | Gap above the page selector (used only when the selector is directly below the panel), and panel size. |
| `ShowWorldRings` | true | Draw zone rings in the world. |
| `RingMaxDistance` | 300 | Hide rings further than this, in meters. |
| `StageStartWindowSeconds` | 30 | Fallback window length, used only if the zone has no timer of its own (normally it does). |
| `HideStageStartZoneAfterWindow` | true | Stop showing the stage-start zone once the window ends. |
| `DebugLogging` / `DumpZonesKey` | false / F8 | Log zone details, for troubleshooting. |
| `DebugAlwaysShow` | false | Show the panel and rings even without a Reincarnation page, for testing. |

## Notes

- The panel names a stage only when the destination is guaranteed. When the game picks it, for example after a soul-binding trial or a Winnowing trial, the panel just says *Continues to the next stage*, so it never reveals anything you wouldn't otherwise know.
- The stage-start reroll shows *Reroll stage N*.

## Building

Requires the .NET 8 SDK and a local Risk of Rain 2 install.

```sh
dotnet build -c Release   # also copies the DLL into the r2modman Default profile
./build.sh                # builds and writes dist/ReincarnationDisplay-<version>.zip for Thunderstore
```

Paths default to the Steam and r2modman locations on Linux. Override `RoR2Dir` and `ProfileDir` in a `Directory.Build.props.user` file (see `Directory.Build.props`).

The version lives in `thunderstore/manifest.json`. To release, bump it, run `./build.sh`, and publish a GitHub release tagged `v<version>` with the zip attached. The publish workflow then uploads that zip to Thunderstore.
