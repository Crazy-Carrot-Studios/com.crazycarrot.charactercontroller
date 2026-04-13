using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

#if UNITY_EDITOR
using UnityEditor;
#endif

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraRig
// Purpose: Cinemachine 3 third-person rig; tuning is serialized on this component (no ScriptableObject profile).
// Required components: CinemachineCamera + CinemachineOrbitalFollow + CinemachineRotationComposer
//          + CinemachineInputAxisController on child "Cinemachine Third Person Follow Cam".
// Placement: CCSCameraRig root (never parented under the player).
// Author: James Schilz
// Date: 2026-04-12
//==============================================================================

namespace CCS.CharacterController
{
    public sealed class CCS_CameraRig : MonoBehaviour
    {
        private const float MouseOrbitVerticalGainRatio = 0.875f;

        #region Variables

        [Header("References")]
        [SerializeField]
        private Transform cameraFollowTarget;

        [SerializeField]
        private Transform cameraLookTarget;

        [SerializeField]
        private Camera mainCamera;

        [SerializeField]
        private CinemachineCamera cinemachineCamera;

        [Header("Character Link")]
        [SerializeField]
        private CCS_CharacterController playerCharacterController;

        [Header("Lens")]
        [SerializeField]
        private float fieldOfView = 58f;

        [SerializeField]
        private float nearClipPlane = 0.1f;

        [SerializeField]
        private float farClipPlane = 5000f;

        [SerializeField]
        private int cinemachinePriority = 20;

        [Header("Orbit")]
        [SerializeField]
        private float orbitRadius = 4f;

        [SerializeField]
        private Vector3 orbitTargetOffset = new Vector3(0.18f, 0f, 0f);

        [SerializeField]
        private float verticalAxisMin = -20f;

        [SerializeField]
        private float verticalAxisCenter = 12f;

        [SerializeField]
        private float verticalAxisMax = 80f;

        [SerializeField]
        private bool verticalAxisWrap;

        [SerializeField]
        private bool horizontalAxisWrap = true;

        [Header("Damping")]
        [SerializeField]
        private Vector3 positionDamping = new Vector3(0.22f, 0.22f, 0.22f);

        [SerializeField]
        private Vector3 rotationDamping = new Vector3(0.28f, 0.28f, 0.28f);

        [SerializeField]
        private Vector2 composerDamping = new Vector2(0.32f, 0.28f);

        [Header("Framing")]
        [SerializeField]
        private Vector3 composerTargetOffset;

        [Header("Orbit Input")]
        [SerializeField]
        [Min(0.01f)]
        private float mouseOrbitSpeed = 0.26f;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            ApplyCinemachineTargets();
            ApplySerializedCameraTuning();
            ApplyOrbitMouseGains();
            ValidateReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyCinemachineTargets();
                ApplySerializedCameraTuning();
                ApplyOrbitMouseGains();
            }
        }
#endif

        #endregion

        #region Public Methods

        /// <summary>Pushes serialized tuning to the vcam, composer, and main camera.</summary>
        public void ApplySerializedCameraTuning()
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
            cinemachineCamera.Priority = cinemachinePriority;

            CinemachineOrbitalFollow orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
                orbitalFollow.Radius = orbitRadius;
                orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
                orbitalFollow.TargetOffset = orbitTargetOffset;

                TrackerSettings tracker = orbitalFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
                tracker.PositionDamping = positionDamping;
                tracker.RotationDamping = rotationDamping;
                orbitalFollow.TrackerSettings = tracker;

                InputAxis vertical = orbitalFollow.VerticalAxis;
                vertical.Range = new Vector2(verticalAxisMin, verticalAxisMax);
                vertical.Wrap = verticalAxisWrap;
                vertical.Center = Mathf.Clamp(
                    verticalAxisCenter,
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
        }

        public void RefreshOrbitMouseGains()
        {
            ApplyOrbitMouseGains();
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

        private void ApplyOrbitMouseGains()
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
            float gainY = -mouseOrbitSpeed * MouseOrbitVerticalGainRatio;

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
                        "[CCS_CameraRig] CinemachineOrbitalFollow is missing on the Cinemachine Third Person Follow Cam.",
                        this);
                }

                if (cinemachineCamera.GetComponent<CinemachineInputAxisController>() == null)
                {
                    Debug.LogError(
                        "[CCS_CameraRig] CinemachineInputAxisController is missing on the vcam.",
                        this);
                }
            }

            if (playerCharacterController == null)
            {
                Debug.LogError("[CCS_CameraRig] Player character controller reference is not assigned.", this);
            }
        }

        #endregion
    }
}
