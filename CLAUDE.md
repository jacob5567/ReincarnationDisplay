<!-- cope:profile -->
## Build and test notes (maintained by COPE; edit freely)
- Check: `dotnet build -c Release -p:CopyToProfile=false`
- Test signal: compile-only: a pass proves the C# compiles against the local RoR2, Unity and RiskOfOptions assemblies; behavior is only checked by running the mod in-game.
- Oracle: not-possible: the code runs only inside Risk of Rain 2 (BepInEx plugin) and there is no test project or harness.
- Hazards: the build's CopyToProfile target copies the dll and pdb into the r2modman profile's BepInEx/plugins, and `-p:CopyToProfile=false` turns that off. Never run `build.sh` (it also packages dist/), and never run the Thunderstore publish workflow. The build needs the game install (`$(HOME)/.local/share/Steam/steamapps/common/Risk of Rain 2`) and RiskOfOptions.dll from the r2modman profile, both outside the repo. Override them with `-p:RoR2Dir=...` and `-p:RiskOfOptionsDll=...`.
- Worktree needs: none (the optional gitignored `Directory.Build.props.user` does not exist here; the game paths are absolute defaults outside the repo)
- Baseline: pass (2026-10-18, f7a9119)
<!-- /cope:profile -->
