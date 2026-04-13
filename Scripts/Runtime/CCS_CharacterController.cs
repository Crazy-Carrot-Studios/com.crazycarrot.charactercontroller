using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_CharacterController
// Purpose: Unity CharacterController locomotion with camera-relative move (LateUpdate),
//          yaw toward move on a visual root. No Animator / Mecanim coupling.
// Required components: UnityEngine.CharacterController on this GameObject.
// Placement: CCSPlayer root (tag Player).
// Author: James Schilz
// Date: 2026-04-12
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
        private UnityEngine.CharacterController characterMotor;

        [SerializeField]
        private Transform cameraFollowTarget;

        [SerializeField]
        private Transform cameraLookTarget;

        [SerializeField]
        private Transform characterVisualRoot;

        [Header("Movement")]
        [SerializeField]
        private float moveSpeed = 4.5f;

        [SerializeField]
        private float rotationSmoothTime = 0.12f;

        [SerializeField]
        private float inputDeadZone = 0.08f;

        [Header("Input")]
        [SerializeField]
        private InputActionReference moveAction;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs;

        private Camera cachedMainCamera;
        private float verticalVelocity;
        private float rotationSmoothVelocity;
        private bool hasMovementInputThisFrame;
        private Vector2 lastMoveInput;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            CacheMainCamera();
            ValidateReferences();
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
                    "[CCS_CharacterController] Move InputActionReference is not assigned. Assign Gameplay/Move from your Input Actions asset.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }
        }

        private void LateUpdate()
        {
            if (characterMotor == null || moveAction == null || moveAction.action == null)
            {
                hasMovementInputThisFrame = false;
                lastMoveInput = Vector2.zero;
                return;
            }

            RefreshGameplayCameraIfNeeded();

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            lastMoveInput = moveInput;
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

        #region Private Methods

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
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }
        }

        /// <summary>
        /// Ensures at least one <see cref="UnityEngine.CharacterController.Move"/> runs so <c>isGrounded</c> can resolve on spawn.
        /// </summary>
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

        /// <summary>Raw Gameplay/Move Vector2 from the last tick.</summary>
        public Vector2 LocomotionMoveInput => lastMoveInput;

        public bool IsMotorGrounded => characterMotor != null && characterMotor.isGrounded;

        public float LocomotionVerticalVelocity => verticalVelocity;

        public float LocomotionYawVelocityDegreesPerSecond => rotationSmoothVelocity;

        #endregion
    }
}
