using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_CharacterController
// Purpose: Baseline third-person movement using Unity CharacterController: camera-relative
//          XZ motion, walk/sprint speed, gravity, smooth yaw toward move direction on a visual root.
//          Optionally drives AC_CCS_BasicLocomotion_Minimal (InputMagnitude, IsGrounded, IsSprinting) or full Invector-style parameters.
// Required components: UnityEngine.CharacterController on this GameObject.
// Placement: CCSPlayer root (tag Player).
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public sealed class CCS_CharacterController : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [SerializeField]
        [Tooltip("Unity CharacterController that performs collision and Move().")]
        private UnityEngine.CharacterController characterMotor;

        [SerializeField]
        [Tooltip("World-space point the follow camera tracks (usually near the head).")]
        private Transform cameraFollowTarget;

        [SerializeField]
        [Tooltip("World-space point the camera aims at (often slightly forward of the follow target).")]
        private Transform cameraLookTarget;

        [SerializeField]
        [Tooltip("Transform rotated toward movement (e.g. CharacterVisuals/ModelOffsetRoot). Leave unassigned to skip facing.")]
        private Transform characterVisualRoot;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Planar speed when moving without sprint input.")]
        private float walkSpeed = 4.5f;

        [SerializeField]
        [Tooltip("Planar speed when sprint is held and there is movement input.")]
        private float sprintSpeed = 8f;

        [SerializeField]
        [Tooltip("Smoothing time for rotating the visual root toward the move direction (seconds).")]
        private float rotationSmoothTime = 0.12f;

        [SerializeField]
        [Tooltip("Move input below this magnitude is treated as no input.")]
        private float inputDeadZone = 0.08f;

        [SerializeField]
        [Tooltip("Gravity acceleration applied along Y while airborne (typically negative, e.g. -30).")]
        private float gravity = -30f;

        [Header("Input")]
        [SerializeField]
        [Tooltip("Input Actions: Gameplay/Move (Vector2).")]
        private InputActionReference moveAction;

        [SerializeField]
        [Tooltip("Input Actions: Gameplay/Sprint (Button). Held + move input uses sprint speed.")]
        private InputActionReference sprintAction;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Humanoid Animator on the visual (usually under VisualRoot). If empty, resolves from Character Visual Root at runtime.")]
        private Animator locomotionAnimator;

        [SerializeField]
        [Tooltip("When enabled, writes locomotion parameters expected by AC_CCS_Locomotion_Base (InputHorizontal, InputVertical, InputMagnitude, IsGrounded, IsSprinting, …).")]
        private bool driveLocomotionAnimator = true;

        [SerializeField]
        [Tooltip(
            "When enabled (Phase 1 default with AC_CCS_BasicLocomotion_Minimal), only InputMagnitude, IsGrounded, and IsSprinting are written. "
            + "When disabled, full Invector-style parameters are written for large controllers.")]
        private bool driveMinimalLocomotionParameterSet;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("When enabled, logs movement diagnostics (can be noisy).")]
        private bool enableDebugLogs;

        private static readonly int AnimatorInputHorizontal = Animator.StringToHash("InputHorizontal");
        private static readonly int AnimatorInputVertical = Animator.StringToHash("InputVertical");
        private static readonly int AnimatorInputDirection = Animator.StringToHash("InputDirection");
        private static readonly int AnimatorInputMagnitude = Animator.StringToHash("InputMagnitude");
        private static readonly int AnimatorRotationMagnitude = Animator.StringToHash("RotationMagnitude");
        private static readonly int AnimatorIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimatorIsSprinting = Animator.StringToHash("IsSprinting");
        private static readonly int AnimatorIsStrafing = Animator.StringToHash("IsStrafing");
        private static readonly int AnimatorVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int AnimatorGroundDistance = Animator.StringToHash("GroundDistance");
        private static readonly int AnimatorGroundAngle = Animator.StringToHash("GroundAngle");
        private static readonly int AnimatorMoveSetId = Animator.StringToHash("MoveSet_ID");

        // Cached gameplay camera for camera-relative axes.
        private Camera cachedMainCamera;
        // Current vertical velocity for CharacterController.Move (gravity).
        private float verticalVelocity;
        // SmoothDampAngle velocity for facing.
        private float rotationSmoothVelocity;
        private bool hasMovementInputThisFrame;
        private Vector2 lastMoveInput;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            CacheMainCamera();
            ValidateReferences();
            ResolveLocomotionAnimator();
        }

        private void Start()
        {
            WarmUpMotorGrounding();
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }
            else
            {
                Debug.LogError(
                    "[CCS_CharacterController] Move InputActionReference is not assigned. Assign Gameplay/Move.",
                    this);
            }

            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.Enable();
            }
            else
            {
                Debug.LogError(
                    "[CCS_CharacterController] Sprint InputActionReference is not assigned. Assign Gameplay/Sprint.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }

            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.Disable();
            }
        }

        private void Update()
        {
            if (!driveLocomotionAnimator)
            {
                return;
            }

            if (!TryBuildLocomotionFrame(out LocomotionFrame frame))
            {
                return;
            }

            UpdateLocomotionAnimatorParameters(frame.MoveInput, frame.PlanarDirection, frame.UseSprint);
        }

        private void LateUpdate()
        {
            if (!TryBuildLocomotionFrame(out LocomotionFrame frame))
            {
                hasMovementInputThisFrame = false;
                lastMoveInput = Vector2.zero;
                return;
            }

            lastMoveInput = frame.MoveInput;
            hasMovementInputThisFrame = frame.HasMovementInput;

            ApplyGravity();
            Vector3 planarVelocity = frame.PlanarDirection * frame.PlanarSpeed;
            Vector3 motion = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z) * Time.deltaTime;
            characterMotor.Move(motion);

            UpdateFacing(frame.PlanarDirection);

            if (enableDebugLogs && hasMovementInputThisFrame)
            {
                Debug.Log(
                    $"[CCS_CharacterController] Move: {frame.MoveInput} | Sprint: {frame.UseSprint} | Speed: {frame.PlanarSpeed}",
                    this);
            }
        }

        #endregion

        #region Private Methods

        private struct LocomotionFrame
        {
            public Vector2 MoveInput;
            public Vector3 PlanarDirection;
            public bool HasMovementInput;
            public bool UseSprint;
            public float PlanarSpeed;
        }

        private bool TryBuildLocomotionFrame(out LocomotionFrame frame)
        {
            frame = default;
            if (characterMotor == null || moveAction == null || moveAction.action == null)
            {
                return false;
            }

            RefreshGameplayCameraIfNeeded();

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            frame.MoveInput = moveInput;
            frame.HasMovementInput = moveInput.sqrMagnitude > inputDeadZone * inputDeadZone;

            Vector3 planarDirection = ComputeCameraRelativePlanarDirection(moveInput);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                planarDirection.Normalize();
            }
            else
            {
                planarDirection = Vector3.zero;
            }

            frame.PlanarDirection = planarDirection;

            bool sprintHeld = sprintAction != null
                && sprintAction.action != null
                && sprintAction.action.IsPressed();
            frame.UseSprint = sprintHeld && frame.HasMovementInput;
            frame.PlanarSpeed = frame.UseSprint ? sprintSpeed : walkSpeed;
            return true;
        }

        private void CacheMainCamera()
        {
            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterController] No camera tagged MainCamera. Movement uses world axes until one exists.",
                    this);
            }
        }

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
                    "[CCS_CharacterController] No MainCamera this frame; planar basis falls back to world axes.",
                    this);
            }
        }

        private void ValidateReferences()
        {
            if (characterMotor == null)
            {
                characterMotor = GetComponent<UnityEngine.CharacterController>();
            }

            if (characterMotor == null)
            {
                Debug.LogError("[CCS_CharacterController] CharacterController is missing on this GameObject.", this);
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

        private void ResolveLocomotionAnimator()
        {
            if (locomotionAnimator != null)
            {
                return;
            }

            if (characterVisualRoot == null)
            {
                return;
            }

            locomotionAnimator = characterVisualRoot.GetComponentInChildren<Animator>(true);
        }

        private void UpdateLocomotionAnimatorParameters(
            Vector2 moveInput,
            Vector3 planarWorldDirection,
            bool sprinting)
        {
            if (locomotionAnimator == null || !locomotionAnimator.isActiveAndEnabled)
            {
                return;
            }

            if (driveMinimalLocomotionParameterSet)
            {
                bool hasMove =
                    moveInput.sqrMagnitude > inputDeadZone * inputDeadZone;
                float blendMagnitude = hasMove ? Mathf.Clamp01(moveInput.magnitude) : 0f;
                if (sprinting && hasMove)
                {
                    blendMagnitude = Mathf.Max(blendMagnitude, 1f);
                }

                bool motorGrounded = characterMotor != null && characterMotor.isGrounded;
                locomotionAnimator.SetFloat(AnimatorInputMagnitude, blendMagnitude);
                locomotionAnimator.SetBool(AnimatorIsGrounded, motorGrounded);
                locomotionAnimator.SetBool(AnimatorIsSprinting, sprinting);
                return;
            }

            float inputMagnitude = hasMovementInputThisFrame ? Mathf.Clamp01(moveInput.magnitude) : 0f;
            float inputHorizontal = 0f;
            float inputVertical = 0f;
            if (characterVisualRoot != null
                && inputMagnitude > 0.001f
                && planarWorldDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 local = characterVisualRoot.InverseTransformDirection(planarWorldDirection);
                inputHorizontal = local.x * inputMagnitude;
                inputVertical = local.z * inputMagnitude;
            }

            float inputDirection = Mathf.Abs(inputHorizontal) < 0.001f && Mathf.Abs(inputVertical) < 0.001f
                ? 0f
                : Mathf.Atan2(inputHorizontal, inputVertical) * Mathf.Rad2Deg;

            bool grounded = characterMotor != null && characterMotor.isGrounded;

            locomotionAnimator.SetFloat(AnimatorInputHorizontal, inputHorizontal);
            locomotionAnimator.SetFloat(AnimatorInputVertical, inputVertical);
            locomotionAnimator.SetFloat(AnimatorInputDirection, inputDirection);
            locomotionAnimator.SetFloat(AnimatorInputMagnitude, inputMagnitude);
            locomotionAnimator.SetFloat(AnimatorRotationMagnitude, 0f);
            locomotionAnimator.SetBool(AnimatorIsGrounded, grounded);
            locomotionAnimator.SetBool(AnimatorIsSprinting, sprinting);
            locomotionAnimator.SetBool(AnimatorIsStrafing, false);
            locomotionAnimator.SetFloat(AnimatorVerticalVelocity, verticalVelocity);
            locomotionAnimator.SetFloat(AnimatorGroundDistance, grounded ? 0f : 1f);
            locomotionAnimator.SetFloat(AnimatorGroundAngle, 0f);
            locomotionAnimator.SetFloat(AnimatorMoveSetId, 0f);
        }

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

        private Vector3 ComputeCameraRelativePlanarDirection(Vector2 moveInput)
        {
            Vector3 cameraForward = GetFlattenedCameraForward();
            Vector3 cameraRight = GetFlattenedCameraRight();
            return cameraRight * moveInput.x + cameraForward * moveInput.y;
        }

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
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        // Runs at least one Move so isGrounded can settle on spawn.
        private void WarmUpMotorGrounding()
        {
            if (characterMotor == null)
            {
                return;
            }

            float probe = Mathf.Max(0.1f, characterMotor.skinWidth * 2.5f);
            characterMotor.Move(new Vector3(0f, -probe, 0f));
            if (characterMotor.isGrounded)
            {
                verticalVelocity = -2f;
                return;
            }

            characterMotor.Move(new Vector3(0f, -probe, 0f));
            if (characterMotor.isGrounded)
            {
                verticalVelocity = -2f;
            }
        }

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

        public bool IsMoving => hasMovementInputThisFrame;

        public Vector2 LastMoveInput => lastMoveInput;

        public bool IsGrounded => characterMotor != null && characterMotor.isGrounded;

        public float VerticalVelocity => verticalVelocity;

        #endregion
    }
}
