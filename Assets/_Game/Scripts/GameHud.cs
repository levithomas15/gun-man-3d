using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GunMan
{
    /// <summary>
    /// Minimal IMGUI HUD: crosshair, weapon + ammo, health bar, hit indicator, death overlay, enemy counter, build panel, key hints.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public WeaponHolder holder;
        public PlayerController player;
        public BuildSystem build;
        public Color crosshairColor = new Color(1f, 1f, 1f, 0.9f);
        public Color healthGood = new Color(0.35f, 0.85f, 0.4f);
        public Color healthBad = new Color(0.9f, 0.2f, 0.15f);

        [Tooltip("Key hints fade out after this many seconds (H shows them again)")]
        public float hintDuration = 20f;

        GUIStyle _big, _small, _hint, _box, _healthLabel, _center;
        Texture2D _white;
        float _hintShownAt;

        void Awake()
        {
            if (holder == null) holder = FindAnyObjectByType<WeaponHolder>();
            if (player == null) player = holder != null ? holder.GetComponentInParent<PlayerController>() : FindAnyObjectByType<PlayerController>();
            if (build == null) build = player != null ? player.GetComponent<BuildSystem>() : FindAnyObjectByType<BuildSystem>();
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _hintShownAt = Time.time;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) _hintShownAt = Time.time;
        }

        void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _big.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
            _small.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true };
            _hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            _box = new GUIStyle(GUI.skin.box);
            _healthLabel = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _healthLabel.normal.textColor = Color.white;
            _center = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _center.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            bool dead = player != null && player.IsDead;

            DrawDamageFeedback(w, h);

            // ---- crosshair ----
            var weapon = holder != null ? holder.Current : null;
            bool building = build != null && build.BuildMode;
            bool aiming = holder != null && holder.IsAiming && !building;
            if (!dead && !aiming)
            {
                float gap = 6f + (weapon != null && !building ? weapon.spreadDegrees * 4f : 0f);
                float len = 10f, thick = 2f;
                var c = GUI.color;
                GUI.color = building && !build.EditMode ? (build.GhostValid ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 0.35f, 0.3f, 0.95f)) : crosshairColor;
                GUI.DrawTexture(new Rect(w / 2 - thick / 2, h / 2 - gap - len, thick, len), _white);
                GUI.DrawTexture(new Rect(w / 2 - thick / 2, h / 2 + gap, thick, len), _white);
                GUI.DrawTexture(new Rect(w / 2 - gap - len, h / 2 - thick / 2, len, thick), _white);
                GUI.DrawTexture(new Rect(w / 2 + gap, h / 2 - thick / 2, len, thick), _white);
                GUI.DrawTexture(new Rect(w / 2 - 1, h / 2 - 1, 2, 2), _white);
                GUI.color = c;
            }
            else if (!dead)
            {
                // simple scope ring
                var c = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                float r = Mathf.Min(w, h) * 0.42f;
                GUI.DrawTexture(new Rect(0, 0, w, h / 2 - r), _white);
                GUI.DrawTexture(new Rect(0, h / 2 + r, w, h / 2 - r), _white);
                GUI.DrawTexture(new Rect(0, 0, w / 2 - r, h), _white);
                GUI.DrawTexture(new Rect(w / 2 + r, 0, w / 2 - r, h), _white);
                GUI.color = crosshairColor;
                GUI.DrawTexture(new Rect(w / 2 - 0.5f, h / 2 - r, 1, r * 2), _white);
                GUI.DrawTexture(new Rect(w / 2 - r, h / 2 - 0.5f, r * 2, 1), _white);
                GUI.color = c;
            }

            if (building && !dead) DrawBuildPanel(w, h);

            // ---- weapon / ammo ----
            if (weapon != null && !dead && !building)
            {
                string ammo = weapon.IsReloading
                    ? $"Nachladen… {Mathf.RoundToInt(weapon.ReloadProgress * 100)}%"
                    : (weapon.infiniteReserve ? $"{weapon.AmmoInMag} / ∞" : $"{weapon.AmmoInMag} / {weapon.Reserve}");
                GUI.Label(new Rect(w - 420, h - 90, 400, 40), weapon.displayName, _big);
                GUI.Label(new Rect(w - 420, h - 50, 400, 30), ammo, _small);

                // weapon slots
                var slotStyle = new GUIStyle(_small) { alignment = TextAnchor.LowerRight, fontSize = 13 };
                float slotW = 82f;
                float x = w - 20 - slotW * holder.Weapons.Count;
                for (int i = 0; i < holder.Weapons.Count; i++)
                {
                    bool active = i == holder.CurrentIndex;
                    slotStyle.normal.textColor = active ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.45f);
                    GUI.Label(new Rect(x, h - 26, slotW - 6, 22), $"{i + 1} {Abbrev(holder.Weapons[i].displayName)}", slotStyle);
                    x += slotW;
                }
            }

            DrawHealth(w, h);

            // ---- score + combat mode ----
            if (holder != null)
            {
                var scoreStyle = new GUIStyle(_small) { alignment = TextAnchor.UpperRight, fontSize = 15 };
                int deaths = player != null ? player.Deaths : 0;
                GUI.Label(new Rect(w - 420, 12, 400, 24), $"Treffer {holder.Hits} · Kills {holder.Kills} · Ziele {PopupTarget.TotalKnockdowns} · Tode {deaths}", scoreStyle);

                var director = CombatDirector.Instance;
                var modeStyle = new GUIStyle(_small) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold, fontSize = 15 };
                string mode;
                if (director == null || !director.CombatMode)
                {
                    modeStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);
                    mode = "Friedlich · K = Kampfmodus";
                }
                else
                {
                    modeStyle.normal.textColor = new Color(1f, 0.55f, 0.3f);
                    var opp = director.Opponent;
                    string who = opp != null && opp.weapon != null
                        ? $"{opp.weapon.displayName}" + (opp.IsEngaged ? " greift an ⚠" : "")
                        : (director.NextOpponentIn > 0.05f ? $"nächster Gegner in {director.NextOpponentIn:0} s" : "suche Gegner…");
                    mode = $"Kampf R{director.Round} · besiegt {director.Defeated} · {who}";
                }
                GUI.Label(new Rect(w - 520, 36, 500, 24), mode, modeStyle);

                // director announcements
                if (director != null && Time.time - director.MessageTime < 3f && !string.IsNullOrEmpty(director.Message))
                {
                    float a = Mathf.Clamp01((3f - (Time.time - director.MessageTime)) / 0.6f);
                    var msg = new GUIStyle(_center) { fontSize = 26 };
                    msg.normal.textColor = new Color(1f, 0.85f, 0.4f, a);
                    GUI.Label(new Rect(0, h * 0.2f, w, 40), director.Message, msg);
                }
            }

            // ---- hints (fade out, H shows them again) ----
            string scene = SceneManager.GetActiveScene().name;
            float hintAge = Time.time - _hintShownAt;
            float hintAlpha = hintDuration <= 0f ? 1f : Mathf.Clamp01((hintDuration - hintAge) / 2f);
            string hints = hintAlpha > 0.01f
                ? $"Karte: {scene}\n" +
                  "WASD bewegen · Shift sprinten · Leertaste springen\n" +
                  "C ducken · Maus zielen · LMB schießen · R nachladen\n" +
                  "RMB zoomen (Sniper) · 1-9 / Mausrad Waffe · Q letzte\n" +
                  "M / F1 / F2 Karte · F5 Respawn · Esc Maus freigeben\n" +
                  "K Kampfmodus: ein Gegner (dunkel gekleidet) greift an\n" +
                  "B Baumodus (Holz): 1-5 Teil · R drehen · LMB bauen · G bearbeiten · X abreißen · T Textur\n" +
                  "Roter Pfeil = Trefferrichtung · H Hilfe ein/aus"
                : $"Karte: {scene} · H Hilfe";
            var hintStyle = new GUIStyle(_hint);
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f * Mathf.Max(hintAlpha, 0.6f));
            GUI.Label(new Rect(12, 10, 520, 170), hints, hintStyle);

            if (dead)
            {
                var c = GUI.color;
                GUI.color = new Color(0.15f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, 0, w, h), _white);
                GUI.color = c;
                float remaining = Mathf.Max(0f, player.respawnDelay - (Time.time - player.DeathTime));
                GUI.Label(new Rect(0, h * 0.4f, w, 50), "Du wurdest erledigt", _center);
                var sub = new GUIStyle(_center) { fontSize = 20, fontStyle = FontStyle.Normal };
                GUI.Label(new Rect(0, h * 0.4f + 52, w, 34), $"Respawn in {remaining:0.0} s  ·  F5 sofort", sub);
            }
            else if (Cursor.lockState != CursorLockMode.Locked)
            {
                var center = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                GUI.Label(new Rect(0, h * 0.6f, w, 40), "Klicken, um weiterzuspielen", center);
            }
        }

        void DrawBuildPanel(float w, float h)
        {
            var accent = new Color(1f, 0.85f, 0.3f);
            var title = new GUIStyle(_big) { fontSize = 26 };
            title.normal.textColor = accent;
            string mode = build.EditMode ? $"Bearbeiten · {BuildGrid.DisplayName(build.Editing.Type)}" : $"Bauen · {BuildGrid.DisplayName(build.SelectedType)}";
            GUI.Label(new Rect(w - 520, h - 118, 500, 36), mode, title);

            string wood = $"Holz {build.MaterialIndex + 1}/{build.MaterialCount} · {build.MaterialName}";
            GUI.Label(new Rect(w - 520, h - 82, 500, 26), wood, _small);

            string keys = build.EditMode
                ? (build.Editing.SubCellCount > 0 ? "LMB Feld an/aus · R drehen · T Holz · X abreißen · G fertig" : "R drehen · T Holz · X abreißen · G fertig")
                : "LMB bauen · R drehen · G bearbeiten · X abreißen · T Holz · B Waffen";
            var keyStyle = new GUIStyle(_small) { fontSize = 13 };
            keyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(w - 520, h - 56, 500, 22), keys, keyStyle);

            // piece slots
            var slotStyle = new GUIStyle(_small) { alignment = TextAnchor.LowerRight, fontSize = 13 };
            float slotW = 84f;
            float x = w - 20 - slotW * BuildGrid.AllTypes.Length;
            for (int i = 0; i < BuildGrid.AllTypes.Length; i++)
            {
                var t = BuildGrid.AllTypes[i];
                bool active = !build.EditMode && t == build.SelectedType;
                slotStyle.normal.textColor = active ? accent : new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(x, h - 26, slotW - 6, 22), $"{i + 1} {BuildGrid.DisplayName(t)}", slotStyle);
                x += slotW;
            }

            // what the crosshair points at
            var target = build.EditMode ? build.Editing : build.Target;
            if (target != null && target.Health != null)
            {
                var info = new GUIStyle(_center) { fontSize = 15, fontStyle = FontStyle.Normal };
                info.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
                string sub = build.EditMode && build.HoverSubCell >= 0 ? $" · Feld {build.HoverSubCell + 1}" + (build.Editing.IsSubCellRemoved(build.HoverSubCell) ? " (offen)" : "") : "";
                GUI.Label(new Rect(0, h / 2 + 26, w, 24), $"{BuildGrid.DisplayName(target.Type)} {Mathf.CeilToInt(target.Health.Current)}/{Mathf.CeilToInt(target.Health.maxHealth)}{sub}", info);
            }
            else if (!build.EditMode && !build.GhostValid && !string.IsNullOrEmpty(build.GhostReason))
            {
                var info = new GUIStyle(_center) { fontSize = 15, fontStyle = FontStyle.Normal };
                info.normal.textColor = new Color(1f, 0.45f, 0.4f, 0.9f);
                GUI.Label(new Rect(0, h / 2 + 26, w, 24), build.GhostReason, info);
            }

            // announcements
            if (Time.time - build.MessageTime < 2.5f && !string.IsNullOrEmpty(build.Message))
            {
                float a = Mathf.Clamp01((2.5f - (Time.time - build.MessageTime)) / 0.5f);
                var msg = new GUIStyle(_center) { fontSize = 20, fontStyle = FontStyle.Normal };
                msg.normal.textColor = new Color(1f, 0.9f, 0.5f, a);
                GUI.Label(new Rect(0, h * 0.26f, w, 30), build.Message, msg);
            }
        }

        void DrawHealth(float w, float h)
        {
            if (player == null || player.Health == null) return;
            var health = player.Health;
            float frac = health.Normalized;
            float x = 20f, y = h - 58f, bw = 280f, bh = 22f;
            var c = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, bw + 8, bh + 8), _white);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(x, y, bw, bh), _white);
            Color bar = Color.Lerp(healthBad, healthGood, frac);
            // pulse when low
            if (frac < 0.3f && !health.IsDead) bar = Color.Lerp(bar, Color.white, Mathf.PingPong(Time.time * 2f, 0.35f));
            GUI.color = bar;
            GUI.DrawTexture(new Rect(x, y, bw * frac, bh), _white);
            GUI.color = c;

            var label = new GUIStyle(_healthLabel);
            GUI.Label(new Rect(x + 8, y - 2, bw, bh + 4), $"{Mathf.CeilToInt(health.Current)}", label);
            var title = new GUIStyle(_hint) { fontSize = 12, alignment = TextAnchor.LowerLeft };
            GUI.Label(new Rect(x, y - 22, 200, 20), "LEBEN", title);
        }

        void DrawDamageFeedback(float w, float h)
        {
            if (player == null) return;
            float since = Time.time - player.LastHitTime;
            const float flashDuration = 0.7f;
            if (since < 0f || since > flashDuration) return;
            float a = 1f - since / flashDuration;
            var c = GUI.color;

            // red vignette: four soft edge bands
            GUI.color = new Color(0.8f, 0f, 0f, 0.28f * a);
            float band = Mathf.Min(w, h) * 0.08f;
            GUI.DrawTexture(new Rect(0, 0, w, band), _white);
            GUI.DrawTexture(new Rect(0, h - band, w, band), _white);
            GUI.DrawTexture(new Rect(0, 0, band, h), _white);
            GUI.DrawTexture(new Rect(w - band, 0, band, h), _white);

            // direction indicator: a red bar on a circle around the crosshair, pointing to the shooter
            float radius = Mathf.Min(w, h) * 0.16f;
            var pivot = new Vector2(w / 2f, h / 2f);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(player.LastHitYaw, pivot);
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.9f * a);
            GUI.DrawTexture(new Rect(w / 2f - 22f, h / 2f - radius - 8f, 44f, 8f), _white);
            GUI.DrawTexture(new Rect(w / 2f - 12f, h / 2f - radius - 16f, 24f, 8f), _white);
            GUI.DrawTexture(new Rect(w / 2f - 4f, h / 2f - radius - 22f, 8f, 6f), _white);
            GUI.matrix = m;
            GUI.color = c;
        }

        static string Abbrev(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= 7 ? name : name.Substring(0, 7);
        }
    }
}
