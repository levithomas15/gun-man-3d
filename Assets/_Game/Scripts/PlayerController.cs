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

        public bool IsGrounded { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool IsCrouching { get; private set; }
        /// <summary>Extra pitch applied by recoil (set by WeaponHolder).</summary>
        public float ExtraPitch { get; set; }

        CharacterController _cc;
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
        }

        public void SetSpawn(Vector3 pos, Quaternion rot)
        {
            _spawnPos = pos;
            _spawnRot = rot;
        }

        public void Respawn()
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            _yaw = _spawnRot.eulerAngles.y;
            _pitch = 0f;
            _velocity = Vector3.zero;
            _impulse = Vector3.zero;
            _cc.enabled = true;
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
            bool inputEnabled = Cursor.lockState == CursorLockMode.Locked;

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
