using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_CharacterController
// Purpose: Production slice — CharacterController locomotion, live camera-relative move
//          (LateUpdate + execution order after CinemachineBrain), yaw from move direction, IsMoving for gameplay.
//          Exposes locomotion snapshots for CCS_AnimatorDriver (no animator writes here).
// Required components: UnityEngine.CharacterController on this GameObject.
// Placement: CCSPlayer root (tag Player).
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    // Runs after CinemachineBrain (default order 0) so planar basis matches this frame’s gameplay camera.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public sealed class CCS_CharacterController : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [Tooltip("Unity CharacterController motor on the player root.")]
        [SerializeField]
        // Unity motor used for Move(), grounding, and collision.
        private UnityEngine.CharacterController characterMotor;

        [Tooltip("Transform the camera rig follows (child under CameraTargets).")]
        [SerializeField]
        // Body tracking point for Cinemachine; not the camera itself.
        private Transform cameraFollowTarget;

        [Tooltip("Transform the camera aims at (child under CameraTargets).")]
        [SerializeField]
        // Aim point for Cinemachine look; slightly offset from follow for framing.
        private Transform cameraLookTarget;

        [Tooltip("Visual root that yaws with movement (model root under CharacterVisuals).")]
        [SerializeField]
        // Rotates toward move direction while moving; idle leaves facing unchanged.
        private Transform characterVisualRoot;

        [Header("Movement")]
        [Tooltip("Planar move speed in meters per second.")]
        [SerializeField]
        // World-space horizontal speed applied along camera-relative input.
        private float moveSpeed = 4.5f;

        [Tooltip("Smoothing time for yaw toward move direction, in seconds.")]
        [SerializeField]
        // SmoothDamp time for rotating the visual root toward travel direction.
        private float rotationSmoothTime = 0.12f;

        [Tooltip("Input magnitude below this is treated as no movement.")]
        [SerializeField]
        // Below this magnitude, input is ignored and IsMoving is false.
        private float inputDeadZone = 0.08f;

        [Header("Input")]
        [Tooltip("Input System action providing Vector2 move (WASD / left stick).")]
        [SerializeField]
        // Gameplay Move action from the CCS input asset.
        private InputActionReference moveAction;

        [Header("Debug")]
        [Tooltip("When enabled, logs verbose movement diagnostics.")]
        [SerializeField]
        // When true, logs per-frame move diagnostics (noisy; dev only).
        private bool enableDebugLogs;

        // Cached Camera.main; game must expose exactly one camera tagged MainCamera.
        private Camera cachedMainCamera;

        // Vertical velocity for CharacterController (gravity and grounding only; no jump).
        private float verticalVelocity;

        // Ref parameter storage for Mathf.SmoothDampAngle on visual root yaw.
        private float rotationSmoothVelocity;

        // True when move input this frame is above the dead zone.
        private bool hasMovementInputThisFrame;

        // Last sampled Gameplay/Move input (for CCS_AnimatorDriver and other readers).
        private Vector2 lastLocomotionMoveInput;

        // Latched when gameplay requests a jump this frame (consumed by animator driver).
        private bool jumpTriggerRequested;

        #endregion

        #region Unity Callbacks

        // Resolves main camera and validates serialized references.
        private void Awake()
        {
            CacheMainCamera();
            ValidateReferences();
        }

        // Enables the Move input action for gameplay.
        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }
            else
            {
                Debug.LogError(
                    "[CCS_CharacterController] Move InputActionReference is not assigned. Assign Gameplay/Move from your Input Actions asset.",
                    this);
            }
        }

        // Disables the Move input action when the component is inactive.
        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }
        }

        // LateUpdate: sample Main Camera after Cinemachine drives it; keeps W/A/S/D aligned with live view.
        private void LateUpdate()
        {
            if (characterMotor == null || moveAction == null || moveAction.action == null)
            {
                hasMovementInputThisFrame = false;
                lastLocomotionMoveInput = Vector2.zero;
                return;
            }

            RefreshGameplayCameraIfNeeded();

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            lastLocomotionMoveInput = moveInput;
            hasMovementInputThisFrame = moveInput.sqrMagnitude > inputDeadZone * inputDeadZone;

            Vector3 planarDirection = ComputeCameraRelativePlanarDirection(moveInput);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                planarDirection.Normalize();
            }
            else
            {
                planarDirection = Vector3.zero;
            }

            ApplyGravity();
            Vector3 planarVelocity = planarDirection * moveSpeed;
            Vector3 motion = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z) * Time.deltaTime;
            characterMotor.Move(motion);

            UpdateFacing(planarDirection);

            if (enableDebugLogs && hasMovementInputThisFrame)
            {
                Debug.Log($"[CCS_CharacterController] Move input: {moveInput} | Planar: {planarDirection}", this);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Notifies the motor that a jump was requested this frame. When jump velocity is implemented,
        /// call this from input; <see cref="CCS_AnimatorDriver"/> consumes it for the Jump trigger.
        /// </summary>
        public void RequestJumpForAnimator()
        {
            jumpTriggerRequested = true;
        }

        /// <summary>
        /// Returns true once if a jump trigger should fire on the Animator (Phase 1: only when requested).
        /// </summary>
        internal bool ConsumeJumpAnimatorTrigger()
        {
            if (!jumpTriggerRequested)
            {
                return false;
            }

            jumpTriggerRequested = false;
            return true;
        }

        #endregion

        #region Private Methods

        // Stores Camera.main once; RefreshGameplayCameraIfNeeded re-resolves if the reference is lost.
        private void CacheMainCamera()
        {
            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterController] No camera tagged MainCamera. Movement cannot be camera-relative until one exists.",
                    this);
            }
        }

        // Re-binds Main Camera when null or destroyed (scene load, wizard, or runtime spawn order).
        private void RefreshGameplayCameraIfNeeded()
        {
            if (cachedMainCamera != null)
            {
                return;
            }

            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null && enableDebugLogs)
            {
                Debug.LogWarning(
                    "[CCS_CharacterController] No MainCamera available this frame; planar basis falls back to world axes.",
                    this);
            }
        }

        // Ensures CharacterController exists and warns when camera or visual refs are missing.
        private void ValidateReferences()
        {
            if (characterMotor == null)
            {
                characterMotor = GetComponent<UnityEngine.CharacterController>();
            }

            if (characterMotor == null)
            {
                Debug.LogError("[CCS_CharacterController] CharacterController component is missing on this GameObject.", this);
            }

            if (cameraFollowTarget == null)
            {
                Debug.LogWarning("[CCS_CharacterController] Camera follow target is not assigned.", this);
            }

            if (cameraLookTarget == null)
            {
                Debug.LogWarning("[CCS_CharacterController] Camera look target is not assigned.", this);
            }

            if (characterVisualRoot == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterController] Character visual root is not assigned; facing will not update.",
                    this);
            }
        }

        // Current-frame flattened forward from gameplay camera (Y = 0); used as move “forward” for input.y.
        private Vector3 GetFlattenedCameraForward()
        {
            if (cachedMainCamera == null)
            {
                return Vector3.forward;
            }

            Vector3 forward = cachedMainCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        // Current-frame flattened right from gameplay camera (Y = 0); used as strafe axis for input.x.
        private Vector3 GetFlattenedCameraRight()
        {
            if (cachedMainCamera == null)
            {
                return Vector3.right;
            }

            Vector3 right = cachedMainCamera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                return Vector3.right;
            }

            return right.normalized;
        }

        // Full camera-relative locomotion: moveDirection = cameraRight * input.x + cameraForward * input.y (XZ).
        private Vector3 ComputeCameraRelativePlanarDirection(Vector2 moveInput)
        {
            Vector3 cameraForward = GetFlattenedCameraForward();
            Vector3 cameraRight = GetFlattenedCameraRight();
            return cameraRight * moveInput.x + cameraForward * moveInput.y;
        }

        // Applies gravity and a small downward stick when grounded so the capsule stays snapped.
        private void ApplyGravity()
        {
            if (characterMotor == null)
            {
                return;
            }

            if (characterMotor.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }
        }

        // Rotates the visual root toward planarDirection only while movement input is active.
        private void UpdateFacing(Vector3 planarDirection)
        {
            if (characterVisualRoot == null)
            {
                return;
            }

            if (!hasMovementInputThisFrame || planarDirection.sqrMagnitude < 0.0001f)
            {
                rotationSmoothVelocity = 0f;
                return;
            }

            float targetYaw = Mathf.Atan2(planarDirection.x, planarDirection.z) * Mathf.Rad2Deg;
            float currentYaw = characterVisualRoot.eulerAngles.y;
            float smoothedYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref rotationSmoothVelocity,
                rotationSmoothTime);
            characterVisualRoot.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
        }

        #endregion

        #region Properties

        // True when move input exceeds dead zone (animation, UI, or other systems).
        public bool IsMoving => hasMovementInputThisFrame;

        /// <summary>Raw Gameplay/Move Vector2 from the last locomotion tick.</summary>
        public Vector2 LocomotionMoveInput => lastLocomotionMoveInput;

        /// <summary>Unity motor grounded flag after the last <c>Move</c> call this frame.</summary>
        public bool IsMotorGrounded => characterMotor != null && characterMotor.isGrounded;

        /// <summary>Vertical velocity used by the motor (gravity), meters per second.</summary>
        public float LocomotionVerticalVelocity => verticalVelocity;

        /// <summary>Yaw smooth-damp angular velocity magnitude (degrees/sec) from facing updates.</summary>
        public float LocomotionYawVelocityDegreesPerSecond => rotationSmoothVelocity;

        /// <summary>Animator on the assigned visual root, if any.</summary>
        public Animator LocomotionAnimator =>
            characterVisualRoot != null ? characterVisualRoot.GetComponent<Animator>() : null;

        #endregion
    }
}
