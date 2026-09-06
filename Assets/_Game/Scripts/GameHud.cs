using UnityEngine;
using UnityEngine.SceneManagement;

namespace GunMan
{
    /// <summary>
    /// Minimal IMGUI HUD: crosshair, weapon + ammo, hit counter, key hints.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public WeaponHolder holder;
        public Color crosshairColor = new Color(1f, 1f, 1f, 0.9f);

        GUIStyle _big, _small, _hint, _box;
        Texture2D _white;

        void Awake()
        {
            if (holder == null) holder = FindAnyObjectByType<WeaponHolder>();
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
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
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            // ---- crosshair ----
            var weapon = holder != null ? holder.Current : null;
            bool aiming = holder != null && holder.IsAiming;
            if (!aiming)
            {
                float gap = 6f + (weapon != null ? weapon.spreadDegrees * 4f : 0f);
                float len = 10f, thick = 2f;
                var c = GUI.color;
                GUI.color = crosshairColor;
                GUI.DrawTexture(new Rect(w / 2 - thick / 2, h / 2 - gap - len, thick, len), _white);
                GUI.DrawTexture(new Rect(w / 2 - thick / 2, h / 2 + gap, thick, len), _white);
                GUI.DrawTexture(new Rect(w / 2 - gap - len, h / 2 - thick / 2, len, thick), _white);
                GUI.DrawTexture(new Rect(w / 2 + gap, h / 2 - thick / 2, len, thick), _white);
                GUI.DrawTexture(new Rect(w / 2 - 1, h / 2 - 1, 2, 2), _white);
                GUI.color = c;
            }
            else
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

            // ---- weapon / ammo ----
            if (weapon != null)
            {
                string ammo = weapon.IsReloading
                    ? $"Nachladen… {Mathf.RoundToInt(weapon.ReloadProgress * 100)}%"
                    : (weapon.infiniteReserve ? $"{weapon.AmmoInMag} / ∞" : $"{weapon.AmmoInMag} / {weapon.Reserve}");
                GUI.Label(new Rect(w - 420, h - 90, 400, 40), weapon.displayName, _big);
                GUI.Label(new Rect(w - 420, h - 50, 400, 30), ammo, _small);

                // weapon slots
                var slotStyle = new GUIStyle(_small) { alignment = TextAnchor.LowerLeft, fontSize = 13 };
                float x = w - 420;
                for (int i = 0; i < holder.Weapons.Count; i++)
                {
                    bool active = i == holder.CurrentIndex;
                    slotStyle.normal.textColor = active ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.45f);
                    GUI.Label(new Rect(x, h - 26, 60, 22), $"{i + 1} {Abbrev(holder.Weapons[i].displayName)}", slotStyle);
                    x += 46;
                }
            }

            // ---- score ----
            if (holder != null)
            {
                var scoreStyle = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                GUI.Label(new Rect(w - 320, 12, 300, 26), $"Treffer: {holder.Hits}   Kills: {holder.Kills}   Ziele: {PopupTarget.TotalKnockdowns}", scoreStyle);
            }

            // ---- hints ----
            string scene = SceneManager.GetActiveScene().name;
            string hints = $"Karte: {scene}\n" +
                           "WASD bewegen · Shift sprinten · Leertaste springen · C ducken\n" +
                           "Maus zielen · LMB schießen · RMB zoomen (Sniper) · R nachladen\n" +
                           "1-9 / Mausrad Waffe wechseln · Q letzte Waffe\n" +
                           "M / F1 / F2 Karte wechseln · F5 Respawn · Esc Maus freigeben";
            GUI.Label(new Rect(12, 10, 520, 120), hints, _hint);

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                var center = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                GUI.Label(new Rect(0, h * 0.6f, w, 40), "Klicken, um weiterzuspielen", center);
            }
        }

        static string Abbrev(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= 5 ? name : name.Substring(0, 5);
        }
    }
}
