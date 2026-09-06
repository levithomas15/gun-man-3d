using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GunMan
{
    /// <summary>
    /// Sits under the player camera. Owns all weapons (children), handles switching, aiming, sway and recoil.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        public Camera playerCamera;
        public float normalFov = 70f;
        public float swayAmount = 0.02f;
        public float bobAmount = 0.015f;
        public float recoilRecovery = 9f;
        public PlayerController player;

        public List<Weapon> Weapons { get; } = new List<Weapon>();
        public Weapon Current => _index >= 0 && _index < Weapons.Count ? Weapons[_index] : null;
        public int CurrentIndex => _index;
        public int Hits { get; private set; }
        public int Kills { get; private set; }
        public bool IsAiming { get; private set; }

        int _index = -1;
        int _lastIndex = 0;
        Vector3 _basePos;
        Vector3 _recoilOffset;
        float _recoilPitch;
        float _bobTime;
        float _equipAnim;

        void Awake()
        {
            _basePos = transform.localPosition;
            Weapons.Clear();
            foreach (Transform child in transform)
            {
                var w = child.GetComponent<Weapon>();
                if (w == null) continue;
                w.Attach(this);
                Weapons.Add(w);
                child.gameObject.SetActive(false);
            }
            if (playerCamera == null) playerCamera = GetComponentInParent<Camera>();
            if (playerCamera != null) normalFov = playerCamera.fieldOfView;
        }

        void Start()
        {
            if (Weapons.Count > 0) Equip(0);
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= Weapons.Count || index == _index) return;
            if (Current != null)
            {
                Current.OnUnequipped();
                Current.gameObject.SetActive(false);
                _lastIndex = _index;
            }
            _index = index;
            Current.gameObject.SetActive(true);
            Current.OnEquipped();
            _equipAnim = 1f;
            IsAiming = false;
            var src = GetComponent<AudioSource>();
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; }
            src.PlayOneShot(ProceduralAudio.Click("equip", 700f, 0.05f), 0.4f);
        }

        public void ApplyRecoil(float kick, float pitch)
        {
            _recoilOffset += new Vector3(Random.Range(-kick * 0.3f, kick * 0.3f), kick * 0.3f, -kick);
            _recoilPitch += pitch;
        }

        public void RegisterHit(bool killed)
        {
            Hits++;
            if (killed) Kills++;
        }

        public void RefillAllAmmo()
        {
            foreach (var w in Weapons) w.RefillFull();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || Weapons.Count == 0) return;

            bool inputEnabled = Cursor.lockState == CursorLockMode.Locked;

            // --- switching ---
            if (inputEnabled)
            {
                for (int i = 0; i < Mathf.Min(9, Weapons.Count); i++)
                {
                    if (DigitPressed(kb, i + 1)) Equip(i);
                }
                if (kb.qKey.wasPressedThisFrame) Equip(_lastIndex);
                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (scroll > 0.01f) Equip((_index + Weapons.Count - 1) % Weapons.Count);
                    else if (scroll < -0.01f) Equip((_index + 1) % Weapons.Count);
                }
            }

            var weapon = Current;
            if (weapon == null) return;

            bool fireHeld = inputEnabled && mouse != null && mouse.leftButton.isPressed;
            bool reload = inputEnabled && kb.rKey.wasPressedThisFrame;
            IsAiming = inputEnabled && mouse != null && mouse.rightButton.isPressed && weapon.zoomFov > 0f && !weapon.IsReloading;

            weapon.Tick(fireHeld, reload, playerCamera);

            // --- view: fov / zoom ---
            if (playerCamera != null)
            {
                float targetFov = IsAiming ? weapon.zoomFov : normalFov;
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * 12f);
            }

            // --- sway / bob / recoil ---
            Vector2 look = (mouse != null && inputEnabled) ? mouse.delta.ReadValue() : Vector2.zero;
            Vector3 sway = new Vector3(-look.x, -look.y, 0f) * swayAmount * 0.01f;
            sway = Vector3.ClampMagnitude(sway, swayAmount);

            float speed = player != null ? player.HorizontalSpeed : 0f;
            bool grounded = player == null || player.IsGrounded;
            if (speed > 0.5f && grounded) _bobTime += Time.deltaTime * (speed > 6f ? 12f : 8f);
            Vector3 bob = new Vector3(Mathf.Sin(_bobTime) * bobAmount, Mathf.Abs(Mathf.Cos(_bobTime)) * bobAmount, 0f) * Mathf.Clamp01(speed / 4f);

            _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, Time.deltaTime * recoilRecovery);
            _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, Time.deltaTime * recoilRecovery);
            _equipAnim = Mathf.Max(0f, _equipAnim - Time.deltaTime * 4f);
            Vector3 equipOffset = Vector3.down * 0.25f * _equipAnim * _equipAnim;

            Vector3 aimOffset = IsAiming ? new Vector3(-_basePos.x, -_basePos.y * 0.3f, -0.05f) : Vector3.zero;

            transform.localPosition = _basePos + sway + bob + _recoilOffset + equipOffset + aimOffset;
            transform.localRotation = Quaternion.Euler(-_recoilPitch * 2f + _equipAnim * 25f, sway.x * 60f, sway.x * 30f);

            if (player != null) player.ExtraPitch = -_recoilPitch;
        }

        static bool DigitPressed(Keyboard kb, int digit)
        {
            return digit switch
            {
                1 => kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame,
                2 => kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame,
                3 => kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame,
                4 => kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame,
                5 => kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame,
                6 => kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame,
                7 => kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame,
                8 => kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame,
                9 => kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame,
                _ => false,
            };
        }
    }
}
