# Gun Man 3D

First-Person-Shooter-Prototyp in **Unity 6 (6000.6.0f1, URP)**.
Zwei Karten, neun Waffen, animierte NPCs (Zivilisten und bewaffnete Soldaten), umschaltbarer Kampfmodus,
Schießstand-Ziele, Munitionskisten und ein Fortnite-artiges Bausystem aus Holz.

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
| K | Kampfmodus an/aus |
| B | Baumodus an/aus (Waffen werden weggesteckt) |
| F5 | Respawn |
| H | Tastenhinweise ein-/ausblenden |
| Esc | Maus freigeben (Klick sperrt sie wieder) |

## Waffen

1. Pistole (`pew`) · 2. MAC-10 · 3. AK-47 · 4. Schrotflinte · 5. AWP Sniper (Zoom) ·
6. Raketenwerfer · 7. Quad-Rakete (4er-Salve) · 8. Granate (Wurf, 2,6 s Zünder) · 9. AK-47 Custom

Hitscan-Waffen hinterlassen Einschusslöcher und Funken, Explosionen haben Radius-Schaden,
Physik-Impuls und Kamerawackeln. Alle Sounds werden zur Laufzeit synthetisiert
(`ProceduralAudio.cs`), es gibt also noch keine Audio-Assets.

## Gegner und Kampfmodus

- **Zivilisten** (bunt) laufen herum und fliehen, wenn man auf sie schießt.
- **Soldaten** (dunkel gekleidet) tragen je eine Waffe aus dem Waffenpack in der rechten Hand
  (Pistole, MAC-10, AK-47, Schrotflinte, AWP). Im normalen Modus patrouillieren sie friedlich.
- **Kampfmodus (K)**: Der `CombatDirector` macht genau *einen* Soldaten zum Gegner. Er verfolgt den Spieler
  über die Karte, hält seine bevorzugte Distanz und schießt in Salven. Stirbt er, übernimmt nach drei Sekunden
  der nächste Soldat mit einer anderen Waffe (Runden-Zähler im HUD). Es greift nie mehr als ein Gegner
  gleichzeitig an. K beendet den Kampfmodus wieder.
- **Spieler-Health**: 100 Lebenspunkte mit Lebensbalken unten links, roter Trefferblitz mit Richtungspfeil,
  Regeneration nach sechs Sekunden ohne Schaden. Bei 0 kippt die Kamera, nach 3,5 s (oder F5) Respawn
  mit vollem Leben und voller Munition. Eigene Explosionen richten nur 30 % Schaden an einem selbst an.
- Balance der NPC-Waffen (Schaden, Streuung, Salven, Pausen) steht in `PrefabBuilder.NpcWeapons`.

## Bauen (B)

Wie in Fortnite, nur mit Holz. Im Baumodus wählt man mit **1–5** (oder Mausrad) das Teil, ein grüner Geist
zeigt, wo es hinkommt, **LMB** setzt es. Alles rastet auf ein 4 m-Raster (4 m Geschosshöhe):

| Taste | Baumodus | Bearbeiten (G auf ein Teil) |
|---|---|---|
| 1 / 2 / 3 / 4 / 5 | Wand / Boden / Decke / Treppe / Dach | – |
| R | drehen (Wand quer, Treppenrichtung) | Teil drehen (Wand spiegeln) |
| LMB | bauen | Feld an/aus (Wand 3×3, Boden/Decke/Dach 2×2) |
| G | Teil unterm Fadenkreuz bearbeiten | fertig |
| X | Teil abreißen | Teil abreißen |
| T / Shift+T | nächste / vorherige Holztextur (40 Stück) | Textur des Teils ändern |

- **Wand** 3×3 Felder: unten Mitte + Mitte entfernen = Tür, nur Mitte = Fenster, obere Reihe weg = halbe Wand.
- **Boden/Decke** sind dieselbe Platte (Decke = Boden des Geschosses darüber), 2×2 Viertel editierbar.
- **Treppe** steigt vom Spieler weg ein Geschoss hoch (Rampe-Collider, begehbar), **Dach** ist eine Pyramide
  in vier Vierteln.
- Ein Boden unter den eigenen Füßen hebt einen hoch; Wände/Treppen/Dächer durch den Spieler sind gesperrt (roter Geist).
- Holz hat **150 Lebenspunkte** und lässt sich mit jeder Waffe zerschießen (Splitter fliegen); NPCs laufen
  um Bauteile herum. Texturen liegen in `Assets/_Game/Textures/Wood`, die Materialien werden vom Builder erzeugt.

## Karten

- **Village** – Dorfplatz mit 16 prozedural zusammengesetzten Häusern aus dem Medieval-Village-Kit,
  Schießstand entlang der Oststraße, 6 Zivilisten + 6 Soldaten, Kisten, Munition.
- **Arena** – ummauerter Hof mit Ecktürmen, Deckungen und Zielreihen, 2 Zivilisten + 8 Soldaten.

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
Assets/_Game/Prefabs      generierte Prefabs (Player, Waffen, NPC + NPC_<waffe>, Ziele, FX)
Assets/_Game/Animation    NPC-AnimatorController (Locomotion + Oberkörper-Ebene mit Pistolenposen), AvatarMask
Assets/_Game/Scenes       generierte Szenen + NavMesh-Daten
Assets/_Game/Materials    Village-/Waffen-/FX-/Bau-Materialien (Build = 40 Holz + Geist/Editier-Overlays)
Assets/_Game/Textures     Holztexturen des Bausystems (40 Free Wood Textures, 512², kachelbar)
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
