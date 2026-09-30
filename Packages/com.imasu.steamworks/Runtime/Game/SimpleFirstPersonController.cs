using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Minimal first-person mover for testing (WASD + Space jump + mouse look).
    /// Owner-only: the local player drives it; NetworkTransform replicates the
    /// result to everyone else.
    /// Movement only runs in the match scene AND while the cursor is locked —
    /// opening the pause menu or settings unlocks the cursor, which halts
    /// input automatically (they own the mouse while open).
    /// </summary>
    public class SimpleFirstPersonController : NetworkBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float sprintMultiplier = 1.6f;
        public float jumpSpeed = 7f;
        public float gravity = 20f;

        [Header("Look")]
        public float sensitivity = 2.5f;
        public float pitchClamp = 89f;

        [Header("Refs")]
        public Transform cameraPivot; // child camera/eye transform

        CharacterController controller;
        float pitch;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            if (isLocalPlayer && cameraPivot != null)
                Cursor.lockState = CursorLockMode.Locked;

            // Apply the config's effective mover values (mode 0 default; per-mode
            // overrides resolved at lobby/map selection elsewhere).
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            if (cfg != null)
            {
                walkSpeed = cfg.EffectiveWalkSpeed();
                sprintMultiplier = cfg.EffectiveSprintMultiplier();
                jumpSpeed = cfg.EffectiveJumpSpeed();
                gravity = cfg.EffectiveGravity();
                sensitivity = cfg.EffectiveMouseSensitivity();
            }
        }

        void Update()
        {
            if (!isLocalPlayer) return;

            if (!InMatchScene())
            {
                // Lobby/menu scenes: just make sure the cursor is usable there.
                return;
            }

            // While any overlay owns the mouse (pause menu, settings), leave the
            // cursor alone entirely — they unlocked it and we must NOT fight them.
            if (ImasuPauseMenu.IsOpen || SettingsMenu.IsOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            // Menus (invite etc.) unlock the cursor; while unlocked, hand mouse +
            // keyboard back to the UI and stop moving. Re-lock once they're gone.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                return;
            }

            HandleLook();
            HandleMove();
        }

        bool InMatchScene()
        {
            ImasuNetworkManager nm = NetworkManager.singleton as ImasuNetworkManager;
            if (nm == null || string.IsNullOrEmpty(nm.matchScene)) return false;
            return SceneManager.GetActiveScene().name == nm.matchScene;
        }

        void HandleLook()
        {
            if (cameraPivot == null) return;

            Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

            // GameSettings sensitivity multiplies the base value, live-applied
            // every frame so the settings slider takes effect immediately.
            float sens = sensitivity * GameSettings.MouseSens;

            float yaw = delta.x * sens * 0.1f;
            pitch -= delta.y * sens * 0.1f;
            pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

            transform.Rotate(Vector3.up, yaw, Space.World);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandleMove()
        {
            if (controller == null) return;

            float speed = walkSpeed;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                speed *= sprintMultiplier;

            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 move = (forward * input.y + right * input.x) * speed;

            // Vertical: gravity + jump.
            Vector3 vel = controller.velocity;
            if (controller.isGrounded && vel.y < 0f)
                vel.y = -1f;

            if (controller.isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                vel.y = jumpSpeed;

            vel.y -= gravity * Time.deltaTime;
            vel.x = move.x;
            vel.z = move.z;

            controller.Move(vel * Time.deltaTime);
        }

        void OnDisable()
        {
            // Never leave the cursor stuck locked if the player object is
            // destroyed/disabled while mid-look (scene change etc.).
            if (isLocalPlayer && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}