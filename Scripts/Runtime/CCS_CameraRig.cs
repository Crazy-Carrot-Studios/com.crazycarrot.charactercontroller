using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

#if UNITY_EDITOR
using UnityEditor;
#endif

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraRig
// Purpose: Cinemachine 3 follow/look targets, flattened camera basis helpers.
//          Orbit input on vcam CinemachineInputAxisController; tuning from optional CCS_CameraProfile.
// Required components: CinemachineCamera + CinemachineOrbitalFollow + CinemachineRotationComposer
//          + CinemachineInputAxisController on child "Cinemachine Third Person Follow Cam".
// Placement: CCSCameraRig root (never parented under the player).
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    public sealed class CCS_CameraRig : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [Tooltip("Transform the Cinemachine body tracks (player CameraFollowTarget).")]
        [SerializeField]
        // Player follow target; written onto CinemachineCamera.Target.TrackingTarget at runtime.
        private Transform cameraFollowTarget;

        [Tooltip("Transform the Cinemachine aim uses (player CameraLookTarget).")]
        [SerializeField]
        // Player look target; written onto CinemachineCamera Target and used by Rotation Composer.
        private Transform cameraLookTarget;

        [Tooltip("Unity camera with CinemachineBrain (typically a child Main Camera).")]
        [SerializeField]
        // Gameplay view camera; flattened forward/right drive character-relative movement feel.
        private Camera mainCamera;

        [Tooltip("Dedicated third-person CinemachineCamera (wizard: child Cinemachine Third Person Follow Cam).")]
        [SerializeField]
        // Virtual camera that owns Orbital Follow, Rotation Composer, and Input Axis Controller.
        private CinemachineCamera cinemachineCamera;

        [Header("Character Link")]
        [Tooltip("CCS character on the player (reference for validation; facing is locomotion-driven only).")]
        [SerializeField]
        // Used to confirm the rig is associated with a player; does not drive orbit input.
        private CCS_CharacterController playerCharacterController;

        [Header("Camera Profile")]
        [Tooltip("Tuning applied to the vcam and main camera. Leave empty only if you set values manually.")]
        [SerializeField]
        // When assigned and Apply Profile On Awake is true, pushes lens, orbit, damping, and mouse speed from the asset.
        private CCS_CameraProfile cameraProfile;

        [Tooltip("When true, Awake applies tuning from cameraProfile (or an in-memory baseline if no asset is assigned). When false, vcam lens/orbit values are left as authored; orbit mouse gains still apply.")]
        [SerializeField]
        // Disable to drive the vcam entirely from the inspector without profile reapplication.
        private bool applyProfileOnAwake = true;

        [Header("Orbit Input")]
        [Tooltip("Mouse look orbit speed on the third-person vcam (overwritten from profile when Apply Profile runs).")]
        [SerializeField]
        [Min(0.01f)]
        // Horizontal gain; vertical uses the same scale with MouseOrbitVerticalGainRatio (inverted).
        private float mouseOrbitSpeed = 0.26f;

        // Vertical orbit gain magnitude relative to horizontal (matches prior wizard asymmetry).
        private const float MouseOrbitVerticalGainRatio = 0.875f;

        // One-time notice when play mode uses an in-memory baseline profile (no asset reference).
        private static bool s_warnedRuntimeInMemoryCameraProfile;

        #endregion

        #region Unity Callbacks

#if UNITY_EDITOR
        // Assigns the package default profile when the component is first added (devs can swap the reference later).
        private void Reset()
        {
            if (cameraProfile != null)
            {
                return;
            }

            cameraProfile = AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(
                CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath);
        }
#endif

        // Pushes targets, optional profile, orbit gains, then validates references.
        private void Awake()
        {
            ApplyCinemachineTargets();

            // Always synthesize baseline data when no asset is assigned so imported scenes / prefabs never hit a
            // null-profile path (orbit gains and optional ApplyProfile still behave predictably).
            if (cameraProfile == null)
            {
                cameraProfile = CCS_CameraProfile.CreateBaselineDefaultsInstance();
                if (applyProfileOnAwake)
                {
                    WarnRuntimeInMemoryCameraProfileOnce();
                }
            }

            if (applyProfileOnAwake)
            {
                ApplyProfile();
            }
            else
            {
                ApplyOrbitMouseGains();
            }

            ValidateReferences();
        }

#if UNITY_EDITOR
        // Keeps orbit gains in sync when mouseOrbitSpeed changes; reapplies profile when the asset reference changes in edit mode.
        private void OnValidate()
        {
            if (cameraProfile != null && !Application.isPlaying)
            {
                ApplyProfile();
            }
            else
            {
                ApplyOrbitMouseGains();
            }
        }
#endif

        #endregion

        #region Public Methods

        // Pushes cameraProfile onto Cinemachine and main camera, then refreshes orbit input gains.
        public void ApplyProfile()
        {
            if (cameraProfile == null || cinemachineCamera == null)
            {
                return;
            }

            LensSettings lens = cinemachineCamera.Lens;
            lens.FieldOfView = cameraProfile.fieldOfView;
            lens.NearClipPlane = cameraProfile.nearClipPlane;
            lens.FarClipPlane = cameraProfile.farClipPlane;
            cinemachineCamera.Lens = lens;
            cinemachineCamera.Priority = cameraProfile.priority;

            CinemachineOrbitalFollow orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
                orbitalFollow.Radius = cameraProfile.orbitRadius;
                orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
                orbitalFollow.TargetOffset = cameraProfile.targetOffset;

                TrackerSettings tracker = orbitalFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
                tracker.PositionDamping = cameraProfile.positionDamping;
                tracker.RotationDamping = cameraProfile.rotationDamping;
                orbitalFollow.TrackerSettings = tracker;

                InputAxis vertical = orbitalFollow.VerticalAxis;
                vertical.Range = new Vector2(cameraProfile.verticalAxisMin, cameraProfile.verticalAxisMax);
                vertical.Wrap = cameraProfile.verticalWrap;
                vertical.Center = Mathf.Clamp(
                    cameraProfile.verticalAxisCenter,
                    vertical.Range.x + 0.01f,
                    vertical.Range.y - 0.01f);
                // Startup: use profile center as the live pitch, not a clamped leftover axis value (avoids wrong tilt at Play).
                vertical.Value = vertical.Center;
                orbitalFollow.VerticalAxis = vertical;

                InputAxis horizontal = orbitalFollow.HorizontalAxis;
                horizontal.Wrap = cameraProfile.horizontalWrap;
                orbitalFollow.HorizontalAxis = horizontal;
            }

            CinemachineRotationComposer rotationComposer = cinemachineCamera.GetComponent<CinemachineRotationComposer>();
            if (rotationComposer != null)
            {
                rotationComposer.CenterOnActivate = true;
                rotationComposer.TargetOffset = cameraProfile.composerTargetOffset;
                rotationComposer.Damping = cameraProfile.composerDamping;
            }

            mouseOrbitSpeed = cameraProfile.mouseOrbitSpeed;
            ApplyOrbitMouseGains();

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = cameraProfile.fieldOfView;
                mainCamera.nearClipPlane = cameraProfile.nearClipPlane;
                mainCamera.farClipPlane = cameraProfile.farClipPlane;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
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

        // Re-applies Look Orbit X/Y gain from mouseOrbitSpeed (used when no profile or after manual speed edits).
        public void RefreshOrbitMouseGains()
        {
            ApplyOrbitMouseGains();
        }

        // World forward from main camera with Y removed; used for camera-relative locomotion.
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

        // World right from main camera with Y removed; strafe basis for locomotion helpers.
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

        private void WarnRuntimeInMemoryCameraProfileOnce()
        {
            if (s_warnedRuntimeInMemoryCameraProfile)
            {
                return;
            }

            s_warnedRuntimeInMemoryCameraProfile = true;
            Debug.LogWarning(
                "[CCS_CameraRig] No CCS_CameraProfile asset on this rig; using in-memory baseline tuning for this play session. "
                + "Assign the package default profile in the Inspector (or open the Character Controller wizard once) so the asset persists.",
                this);
        }

        // Writes mouseOrbitSpeed onto CinemachineInputAxisController Look Orbit X/Y gains.
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

        // Assigns follow and look transforms to the CinemachineCamera Target block.
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

        // Logs missing or invalid rig references (no orbit axes; vertical limits live on Orbital Follow).
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
                        "[CCS_CameraRig] CinemachineInputAxisController is missing on the vcam; add it to drive orbit from the CCS Look action.",
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
