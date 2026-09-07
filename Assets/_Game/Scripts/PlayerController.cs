using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GunMan
{
    /// <summary>
    /// First person character controller: WASD / arrow keys, mouse look, sprint (Shift), jump (Space), crouch (C / Ctrl).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 5f;
        public float sprintSpeed = 8.5f;
        public float crouchSpeed = 2.5f;
        public float jumpHeight = 1.25f;
        public float gravity = -22f;
        public float mouseSensitivity = 0.09f;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;
        public Transform cameraPivot;
        [Tooltip("Seconds after death until the player respawns automatically")]
        public float respawnDelay = 3.5f;

        public bool IsGrounded { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        /// <summary>Extra pitch applied by recoil (set by WeaponHolder).</summary>
        public float ExtraPitch { get; set; }
        public Health Health { get; private set; }
        public bool IsDead => Health != null && Health.IsDead;
        /// <summary>Time.time of the last hit taken (HUD flash).</summary>
        public float LastHitTime { get; private set; } = -999f;
        /// <summary>Yaw (degrees, relative to the view direction) the last hit came from. 0 = straight ahead.</summary>
        public float LastHitYaw { get; private set; }
        public float DeathTime { get; private set; }
        public int Deaths { get; private set; }

        CharacterController _cc;
        WeaponHolder _holder;
        Coroutine _respawnRoutine;
        float _pitch;
        float _yaw;
        Vector3 _velocity;
        Vector3 _impulse;
        Vector3 _spawnPos;
        Quaternion _spawnRot;
        float _cameraStandY;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
            if (cameraPivot != null) _cameraStandY = cameraPivot.localPosition.y;
            _holder = GetComponentInChildren<WeaponHolder>(true);
            Health = GetComponent<Health>();
            if (Health != null)
            {
                Health.onDamaged.AddListener(OnDamaged);
                Health.onDied.AddListener(OnDied);
            }
        }

        void OnDamaged(DamageInfo info)
        {
            LastHitTime = Time.time;
            Vector3 from = info.Source != null ? info.Source.transform.position - transform.position : -info.Direction;
            from.y = 0f;
            if (from.sqrMagnitude > 0.001f)
                LastHitYaw = Vector3.SignedAngle(transform.forward, from.normalized, Vector3.up);
            if (info.Amount > 0f)
            {
                CameraShake.Shake(Mathf.Clamp(info.Amount / 60f, 0.05f, 0.25f), 0.2f, transform.position, 10f);
                FxLibrary.PlayAt(ProceduralAudio.Impact(), cameraPivot != null ? cameraPivot.position : transform.position, 0.6f, 0.6f);
            }
        }

        void OnDied(DamageInfo info)
        {
            Deaths++;
            DeathTime = Time.time;
            SetCrouch(false);
            if (_holder != null) _holder.gameObject.SetActive(false);
            if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnAfter(respawnDelay));
        }

        IEnumerator RespawnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _respawnRoutine = null;
            Respawn();
        }

        public void SetSpawn(Vector3 pos, Quaternion rot)
        {
            _spawnPos = pos;
            _spawnRot = rot;
        }

        public void Respawn()
        {
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }
            _cc.enabled = false;
            transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            _yaw = _spawnRot.eulerAngles.y;
            _pitch = 0f;
            _velocity = Vector3.zero;
            _impulse = Vector3.zero;
            _cc.enabled = true;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            if (Health != null) Health.Revive();
            if (_holder != null)
            {
                _holder.gameObject.SetActive(true);
                _holder.RefillAllAmmo();
            }
        }

        public void AddImpulse(Vector3 impulse)
        {
            _impulse += impulse;
            if (impulse.y > 0f) _velocity.y = Mathf.Max(_velocity.y, impulse.y);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;
            bool inputEnabled = Cursor.lockState == CursorLockMode.Locked && !IsDead;

            if (IsDead)
            {
                // death cam: sink towards the ground and tilt
                float t = Mathf.Clamp01((Time.time - DeathTime) / 0.9f);
                if (cameraPivot != null)
                {
                    var p = cameraPivot.localPosition;
                    p.y = Mathf.Lerp(_cameraStandY, 0.45f, t);
                    cameraPivot.localPosition = p;
                    cameraPivot.localRotation = Quaternion.Euler(Mathf.Lerp(_pitch, 8f, t), 0f, Mathf.Lerp(0f, 55f, t));
                }
                _velocity.y = Mathf.Max(_velocity.y + gravity * Time.deltaTime, -12f);
                if (_cc.enabled) _cc.Move(Vector3.up * _velocity.y * Time.deltaTime);
                return;
            }

            // ---- look ----
            if (inputEnabled && mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                _yaw += d.x * mouseSensitivity;
                _pitch -= d.y * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch + ExtraPitch, 0f, 0f);

            // ---- move ----
            float x = 0f, z = 0f;
            if (inputEnabled)
            {
                x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            }
            Vector3 move = transform.right * x + transform.forward * z;
            if (move.sqrMagnitude > 1f) move.Normalize();

            bool wantsCrouch = inputEnabled && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
            SetCrouch(wantsCrouch);

            float speed = IsCrouching ? crouchSpeed : (inputEnabled && kb.leftShiftKey.isPressed && z > 0.1f ? sprintSpeed : walkSpeed);

            IsGrounded = _cc.isGrounded;
            if (IsGrounded && _velocity.y < 0f) _velocity.y = -2f;
            if (IsGrounded && inputEnabled && kb.spaceKey.wasPressedThisFrame && !IsCrouching)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _velocity.y += gravity * Time.deltaTime;
            _impulse = Vector3.Lerp(_impulse, Vector3.zero, Time.deltaTime * 4f);

            Vector3 frameMove = move * speed + new Vector3(_impulse.x, 0f, _impulse.z) + Vector3.up * _velocity.y;
            _cc.Move(frameMove * Time.deltaTime);
            HorizontalSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;

            if (transform.position.y < -40f) Respawn();
        }

        void SetCrouch(bool crouch)
        {
            if (crouch == IsCrouching) return;
            if (!crouch)
            {
                // check headroom
                if (Physics.SphereCast(transform.position + Vector3.up * (crouchHeight - 0.3f), 0.3f, Vector3.up, out _, standHeight - crouchHeight + 0.05f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                    return;
            }
            IsCrouching = crouch;
            float h = crouch ? crouchHeight : standHeight;
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
            if (cameraPivot != null)
            {
                var p = cameraPivot.localPosition;
                p.y = crouch ? _cameraStandY - (standHeight - crouchHeight) : _cameraStandY;
                cameraPivot.localPosition = p;
            }
        }
    }
}
