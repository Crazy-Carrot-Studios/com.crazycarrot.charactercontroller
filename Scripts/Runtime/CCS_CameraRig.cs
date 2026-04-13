using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

#if UNITY_EDITOR
using UnityEditor;
#endif

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraRig
// Purpose: Cinemachine 3 third-person orbit camera. All tuning is serialized on this
//          component; no profiles or external camera assets required.
// Required components: CinemachineCamera + CinemachineOrbitalFollow + CinemachineRotationComposer
//          + CinemachineInputAxisController on child "Cinemachine Third Person Follow Cam".
// Placement: CCSCameraRig root (not parented under the player).
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    public sealed class CCS_CameraRig : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [SerializeField]
        [Tooltip("Transform the orbit camera follows (e.g. CameraFollowTarget on the player).")]
        private Transform cameraFollowTarget;

        [SerializeField]
        [Tooltip("Transform the camera aims at (e.g. CameraLookTarget on the player).")]
        private Transform cameraLookTarget;

        [SerializeField]
        [Tooltip("Unity camera with CinemachineBrain that renders the game view.")]
        private Camera mainCamera;

        [SerializeField]
        [Tooltip("Third-person CinemachineCamera (vcam) driving orbit and aim.")]
        private CinemachineCamera cinemachineCamera;

        [SerializeField]
        [Tooltip("Player CharacterController script for optional cross-links and validation.")]
        private CCS_CharacterController playerCharacterController;

        [Header("Orbit Settings")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Base mouse/gamepad sensitivity applied to Look Orbit X on the Cinemachine Input Axis Controller.")]
        private float mouseOrbitSpeed = 0.26f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Multiplier for vertical orbit gain relative to horizontal (Look Orbit Y uses mouseOrbitSpeed * this).")]
        private float verticalOrbitSpeedMultiplier = 0.875f;

        [SerializeField]
        [Tooltip("Distance from the follow target in sphere orbit mode.")]
        private float orbitRadius = 4f;

        [SerializeField]
        [Tooltip("Local offset from the follow target used by orbital follow (e.g. slight over-shoulder).")]
        private Vector3 targetOffset = new Vector3(0.18f, 0f, 0f);

        [Header("Pitch Limits")]
        [SerializeField]
        [Tooltip("Minimum vertical orbit angle in degrees.")]
        private float minVerticalAngle = -20f;

        [SerializeField]
        [Tooltip("Maximum vertical orbit angle in degrees.")]
        private float maxVerticalAngle = 80f;

        [SerializeField]
        [Tooltip("Starting vertical angle in degrees (clamped between min and max).")]
        private float defaultVerticalAngle = 12f;

        [Header("Orbit Axes")]
        [SerializeField]
        [Tooltip("When true, horizontal orbit wraps (infinite yaw).")]
        private bool horizontalAxisWrap = true;

        [SerializeField]
        [Tooltip("When true, vertical axis wraps (unusual for third-person; usually off).")]
        private bool verticalAxisWrap;

        [Header("Camera")]
        [SerializeField]
        [Tooltip("Field of view on the vcam lens and main camera.")]
        private float fieldOfView = 58f;

        [SerializeField]
        [Tooltip("Near clip plane on the vcam lens and main camera.")]
        private float nearClipPlane = 0.1f;

        [SerializeField]
        [Tooltip("Far clip plane on the vcam lens and main camera.")]
        private float farClipPlane = 5000f;

        [SerializeField]
        [Tooltip("Cinemachine virtual camera priority (higher wins over other vcams).")]
        private int priority = 20;

        [Header("Damping")]
        [SerializeField]
        [Tooltip("Orbital follow position damping in tracker settings.")]
        private Vector3 positionDamping = new Vector3(0.22f, 0.22f, 0.22f);

        [SerializeField]
        [Tooltip("Orbital follow rotation damping in tracker settings.")]
        private Vector3 rotationDamping = new Vector3(0.28f, 0.28f, 0.28f);

        [SerializeField]
        [Tooltip("Rotation composer screen-space damping (x, y).")]
        private Vector2 composerDamping = new Vector2(0.32f, 0.28f);

        [SerializeField]
        [Tooltip("Aim target offset applied by the rotation composer.")]
        private Vector3 composerTargetOffset;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            ApplyCinemachineTargets();
            ApplySerializedRigSettings();
            ApplyOrbitInputGains();
            ValidateReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyCinemachineTargets();
                ApplySerializedRigSettings();
                ApplyOrbitInputGains();
            }
        }
#endif

        #endregion

        #region Public Methods

        // Pushes all serialized fields to the vcam, orbital follow, composer, input gains, and main camera.
        public void ApplySerializedRigSettings()
        {
            if (cinemachineCamera == null)
            {
                return;
            }

            LensSettings lens = cinemachineCamera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = nearClipPlane;
            lens.FarClipPlane = farClipPlane;
            cinemachineCamera.Lens = lens;
            cinemachineCamera.Priority = priority;

            CinemachineOrbitalFollow orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
                orbitalFollow.Radius = orbitRadius;
                orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
                orbitalFollow.TargetOffset = targetOffset;

                TrackerSettings tracker = orbitalFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
                tracker.PositionDamping = positionDamping;
                tracker.RotationDamping = rotationDamping;
                orbitalFollow.TrackerSettings = tracker;

                InputAxis vertical = orbitalFollow.VerticalAxis;
                vertical.Range = new Vector2(minVerticalAngle, maxVerticalAngle);
                vertical.Wrap = verticalAxisWrap;
                vertical.Center = Mathf.Clamp(
                    defaultVerticalAngle,
                    vertical.Range.x + 0.01f,
                    vertical.Range.y - 0.01f);
                vertical.Value = vertical.Center;
                orbitalFollow.VerticalAxis = vertical;

                InputAxis horizontal = orbitalFollow.HorizontalAxis;
                horizontal.Wrap = horizontalAxisWrap;
                orbitalFollow.HorizontalAxis = horizontal;
            }

            CinemachineRotationComposer rotationComposer = cinemachineCamera.GetComponent<CinemachineRotationComposer>();
            if (rotationComposer != null)
            {
                rotationComposer.CenterOnActivate = true;
                rotationComposer.TargetOffset = composerTargetOffset;
                rotationComposer.Damping = composerDamping;
            }

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = fieldOfView;
                mainCamera.nearClipPlane = nearClipPlane;
                mainCamera.farClipPlane = farClipPlane;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(cinemachineCamera);
                if (orbitalFollow != null)
                {
                    EditorUtility.SetDirty(orbitalFollow);
                }

                if (rotationComposer != null)
                {
                    EditorUtility.SetDirty(rotationComposer);
                }

                if (mainCamera != null)
                {
                    EditorUtility.SetDirty(mainCamera);
                }
            }
#endif

            ApplyOrbitInputGains();
        }

        // Re-applies mouse/gamepad gains on the Cinemachine Input Axis Controller (safe to call after inspector edits).
        public void RefreshOrbitInputGains()
        {
            ApplyOrbitInputGains();
        }

        public Vector3 GetFlattenedCameraForward()
        {
            if (mainCamera == null)
            {
                return Vector3.forward;
            }

            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        public Vector3 GetFlattenedCameraRight()
        {
            if (mainCamera == null)
            {
                return Vector3.right;
            }

            Vector3 right = mainCamera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                return Vector3.right;
            }

            return right.normalized;
        }

        #endregion

        #region Private Methods

        private void ApplyOrbitInputGains()
        {
            if (cinemachineCamera == null)
            {
                return;
            }

            CinemachineInputAxisController axisController = cinemachineCamera.GetComponent<CinemachineInputAxisController>();
            if (axisController == null)
            {
                return;
            }

            axisController.SynchronizeControllers();

            float gainX = mouseOrbitSpeed;
            float gainY = -mouseOrbitSpeed * verticalOrbitSpeedMultiplier;

            for (int i = 0; i < axisController.Controllers.Count; i++)
            {
                InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller controller =
                    axisController.Controllers[i];
                if (controller == null || controller.Input == null)
                {
                    continue;
                }

                if (controller.Name == "Look Orbit X")
                {
                    controller.Input.Gain = gainX;
                }
                else if (controller.Name == "Look Orbit Y")
                {
                    controller.Input.Gain = gainY;
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(axisController);
            }
#endif
        }

        private void ApplyCinemachineTargets()
        {
            if (cinemachineCamera == null)
            {
                return;
            }

            CameraTarget target = cinemachineCamera.Target;
            target.TrackingTarget = cameraFollowTarget;
            target.LookAtTarget = cameraLookTarget;
            target.CustomLookAtTarget = cameraLookTarget != null;
            cinemachineCamera.Target = target;
        }

        private void ValidateReferences()
        {
            if (cameraFollowTarget == null)
            {
                Debug.LogError("[CCS_CameraRig] Camera follow target is not assigned.", this);
            }

            if (cameraLookTarget == null)
            {
                Debug.LogWarning(
                    "[CCS_CameraRig] Camera look target is not assigned; aim pipeline may be invalid.",
                    this);
            }

            if (mainCamera == null)
            {
                Debug.LogError("[CCS_CameraRig] Main camera is not assigned.", this);
            }
            else if (mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                Debug.LogWarning(
                    "[CCS_CameraRig] Main camera has no CinemachineBrain; the view may not update from Cinemachine.",
                    this);
            }

            if (cinemachineCamera == null)
            {
                Debug.LogError("[CCS_CameraRig] CinemachineCamera is not assigned.", this);
            }
            else
            {
                if (cinemachineCamera.GetComponent<CinemachineOrbitalFollow>() == null)
                {
                    Debug.LogError(
                        "[CCS_CameraRig] CinemachineOrbitalFollow is missing on the vcam GameObject.",
                        this);
                }

                if (cinemachineCamera.GetComponent<CinemachineInputAxisController>() == null)
                {
                    Debug.LogError(
                        "[CCS_CameraRig] CinemachineInputAxisController is missing on the vcam GameObject.",
                        this);
                }
            }

            if (playerCharacterController == null)
            {
                Debug.LogWarning(
                    "[CCS_CameraRig] Player character controller is not assigned (optional but recommended).",
                    this);
            }
        }

        #endregion
    }
}
