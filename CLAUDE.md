# CLAUDE.md – gun-man-3d

Unity 6 (6000.6.0f1) URP First-Person-Shooter-Prototyp. Sprache mit dem Nutzer: Deutsch.

## Arbeiten mit dem Editor

- Unity CLI: `~/.unity/bin/unity`. Vor Szenen-/Asset-Änderungen `unity status` prüfen. Läuft der Editor,
  Änderungen live über `unity command <name> --param value --project-path <dieses Projekt>` machen
  (immer `--project-path` mitgeben, auf diesem Rechner läuft oft parallel das Projekt `autorennen`).
- MCP-Server `unity-editor-mcp` ist in `.mcp.json` auf dieses Projekt gepinnt; Skill: `.claude/skills/unity-cli`.
- Nach Script-Änderungen: `unity command recompile`, dann `recompile_status` pollen.
- Content neu bauen: `unity command menu --path "GunMan/2. Build Game Content"` (oder headless mit
  `-executeMethod GunMan.EditorTools.GunManBuilder.BuildAll`). Beide Szenen, alle Prefabs und NavMeshes
  werden dabei komplett neu erzeugt – manuelle Szenenänderungen gehen verloren, also Layout-Änderungen
  im Builder-Code (`Assets/_Game/Editor/SceneBuilder.cs`, `KitBuilder.cs`) machen.
- Tests: `unity command run_tests --mode PlayMode` + `test_status`, oder headless `-runTests -testPlatform PlayMode`.
- Screenshots: `unity command capture_game_view --source screen --save_path <pfad>` (Pfad ist relativ zu `Assets/`,
  Ergebnis danach aus `Assets/` herausnehmen) oder headless `GunManBuilder.RenderPreviews` (`$GUNMAN_PREVIEW_DIR`).
- Headless-Builds nicht starten, solange der Editor das Projekt offen hat (Projekt-Lock).

## Code-Konventionen / Stolperfallen

- Jede MonoBehaviour-Klasse in einer Datei mit gleichem Namen (sonst verliert Unity die Script-Referenz im Prefab).
- Assemblies: `GunMan.Runtime` (Scripts), `GunMan.Editor` (Editor), `GunMan.Tests` (Tests).
- Input ausschließlich über das neue Input System (`Keyboard.current`, `Mouse.current`), `activeInputHandler = 1`.
- FBX-Wurzelknoten der Styloo-Waffen haben Scale 100 – beim Einpassen `asset.transform.localScale` mitmultiplizieren
  (siehe `PrefabBuilder.FitModel`). Kit-Teile behalten ihre Import-Rotation (`KitBuilder.Place`).
- Bounds im Batch-/Edit-Modus immer über `BuildUtil.WorldBounds` (Mesh-basiert) berechnen, nicht `Renderer.bounds`.
- Village-Materialien werden per Name (`MI_*`) in `GunManAssetPostprocessor.OnAssignMaterialModel` zugeordnet;
  Waffen-Materialien liegen extrahiert in `Assets/_Game/Materials/Guns`.
- Sounds sind synthetisch (`ProceduralAudio`); wenn Audio-Assets dazukommen, `Weapon.PlayShotSound` anpassen.
- NPC-Waffen hängen am Knochen `hand_r`. Die Griff-Lage wird beim Prefab-Bau berechnet, indem der Clip
  `Pistol_Aim_Neutral` per `AnimationMode` gesampelt wird (`PrefabBuilder.AttachWeaponToHand`); Feintuning über
  `NpcWeaponDef.gripRotation/gripOffset`. `Animator.Update` funktioniert im Edit-Modus dafür nicht.
- Nur ein Gegner greift gleichzeitig an: `CombatDirector` (auf dem GameManager-Objekt) wählt im Kampfmodus (K)
  den aktiven Gegner, `NpcCharacter` fragt `IsOpponent` ab. Ohne Kampfmodus schießt kein NPC.
- Waffen-Raycasts ignorieren den Besitzer über `Weapon.RaycastIgnoring(..., ownerTransform)` mit `IsChildOf` –
  nicht `transform.root` verwenden, alle Gameplay-Objekte hängen unter `Gameplay`.
- Editor-Log des per Hub gestarteten Editors liegt in `Logs/Editor.log` im Projekt (nicht `~/Library/Logs/Unity`).
  `capture_game_view --save_path` darf kein `..` enthalten. Vor `menu`-Builds Play-Modus mit `editor_stop` beenden.

## Offen / Ideen

- Zweites Map-Paket des Nutzers fehlte beim Setup; `Arena` ist ein Platzhalter aus dem Medieval-Kit.
- Nicht genutzte Modelle aus dem Waffenpack: `flashbang_low`, `smoke_low`, `incendiary_low`, `rocketlaunchervariant`,
  `nadevariant_low`, `board`, `bullet*`.
