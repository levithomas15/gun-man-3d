using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GunMan
{
    /// <summary>
    /// Cursor lock, map switching (M / F1..F2), respawn (F5).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public PlayerController player;
        public bool lockCursorOnStart = true;

        void Awake()
        {
            Instance = this;
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }

        void Start()
        {
            if (lockCursorOnStart && Application.isFocused) LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
                LockCursor(true);

            if (kb.mKey.wasPressedThisFrame) LoadNextMap();
            if (kb.f1Key.wasPressedThisFrame) LoadMap(0);
            if (kb.f2Key.wasPressedThisFrame) LoadMap(1);
            if (kb.f5Key.wasPressedThisFrame && player != null) player.Respawn();
        }

        public static void LoadNextMap()
        {
            int count = SceneManager.sceneCountInBuildSettings;
            if (count <= 1) return;
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % count;
            SceneManager.LoadScene(next);
        }

        public static void LoadMap(int index)
        {
            if (index < 0 || index >= SceneManager.sceneCountInBuildSettings) return;
            if (SceneManager.GetActiveScene().buildIndex == index) return;
            SceneManager.LoadScene(index);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && lockCursorOnStart) LockCursor(true);
        }
    }
}
