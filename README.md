# Gun Man 3D

First-Person-Shooter-Prototyp in **Unity 6 (6000.6.0f1, URP)**.
Zwei Karten, neun Waffen, animierte NPC-Dummies, Schießstand-Ziele, Munitionskisten.

## Steuerung

| Taste | Aktion |
|---|---|
| WASD / Pfeile | Bewegen |
| Maus | Umsehen |
| Shift | Sprinten |
| Leertaste | Springen |
| C / Strg | Ducken |
| Linke Maustaste | Schießen |
| Rechte Maustaste | Zoomen (Sniper) |
| R | Nachladen |
| 1–9, Mausrad, Q | Waffe wechseln / letzte Waffe |
| M, F1, F2 | Karte wechseln |
| F5 | Respawn |
| Esc | Maus freigeben (Klick sperrt sie wieder) |

## Waffen

1. Pistole (`pew`) · 2. MAC-10 · 3. AK-47 · 4. Schrotflinte · 5. AWP Sniper (Zoom) ·
6. Raketenwerfer · 7. Quad-Rakete (4er-Salve) · 8. Granate (Wurf, 2,6 s Zünder) · 9. AK-47 Custom

Hitscan-Waffen hinterlassen Einschusslöcher und Funken, Explosionen haben Radius-Schaden,
Physik-Impuls und Kamerawackeln. Alle Sounds werden zur Laufzeit synthetisiert
(`ProceduralAudio.cs`), es gibt also noch keine Audio-Assets.

## Karten

- **Village** – Dorfplatz mit 16 prozedural zusammengesetzten Häusern aus dem Medieval-Village-Kit,
  Schießstand entlang der Oststraße, 12 NPCs, Kisten, Munition.
- **Arena** – ummauerter Hof mit Ecktürmen, Deckungen und Zielreihen.

Beide Szenen werden komplett per Editor-Skript erzeugt (siehe unten) und liegen in `Assets/_Game/Scenes`.

## Verwendete Packs (`Assets/ThirdParty`)

| Pack | Inhalt | Lizenz |
|---|---|---|
| Styloo Guns Asset Pack V1.1 | Waffen, Munition, Granaten (FBX + eingebettete Texturen) | siehe `README_Styloo.txt`, itch.io |
| Quaternius Universal Animation Library (Standard) | Humanoid-Mannequin + 43 Animationen | CC0 |
| Quaternius Medieval Village MegaKit (Standard) | 176 modulare Bauteile + Texturen | CC0 |

## Projektstruktur

```
Assets/_Game/Scripts      Laufzeit-Code (GunMan.Runtime)
Assets/_Game/Editor       Import-Regeln + Content-Builder (GunMan.Editor)
Assets/_Game/Tests        PlayMode-Smoke-Tests
Assets/_Game/Prefabs      generierte Prefabs (Player, Waffen, NPC, Ziele, FX)
Assets/_Game/Scenes       generierte Szenen + NavMesh-Daten
Assets/_Game/Materials    Village-/Waffen-/FX-Materialien
Assets/ThirdParty         die drei Asset-Packs
```

## Content neu erzeugen

Alles außer den Rohdaten der Packs ist reproduzierbar:

1. **GunMan → 1. Prepare Assets** (`GunManBootstrap.Prepare`) – Village-Materialien, Waffen-Texturen/-Materialien
   extrahieren, Animations-Clips konfigurieren.
2. **GunMan → 2. Build Game Content** (`GunManBuilder.BuildAll`) – FX, Prefabs, beide Szenen, NavMesh, Build-Settings.
3. **GunMan → 3. Render Previews** – Screenshots nach `Logs/previews` (oder `$GUNMAN_PREVIEW_DIR`).

Headless:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -quit -projectPath . -executeMethod GunMan.EditorTools.GunManBuilder.BuildAndPreview -logFile -
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml -logFile -
```

## Unity CLI / MCP

Das Projekt ist für das offizielle Unity-CLI (`~/.unity/bin/unity`) eingerichtet:

- `.mcp.json` registriert den MCP-Server `unity-editor-mcp` (`unity mcp --project-path …`) für Claude Code.
- `Packages/manifest.json` enthält `com.unity.pipeline`, das die Editor-Kommandos für CLI und MCP bereitstellt.
- `.claude/skills/unity-cli` ist der passende Agent-Skill.

Bei laufendem Editor: `unity status`, `unity command` (Liste), `unity command editor_play`, `unity command eval '<C#>'`.
