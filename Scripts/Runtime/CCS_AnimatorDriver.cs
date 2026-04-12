using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_AnimatorDriver
// Purpose: Phase 1 — reads locomotion state from CCS_CharacterController each frame
//          and pushes the matching parameters into the Animator (no root motion).
// Required components: CCS_CharacterController on the same GameObject (or assigned).
// Placement: CCSPlayer root next to CCS_CharacterController; Animator on visual root.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    /// <summary>
    /// Single owner for writing gameplay-driven values into the locomotion Animator.
    /// Expects parameter names to match <c>CCS_Base_locomotion_controller</c> (InputMagnitude, etc.).
    /// Runs after <see cref="CCS_CharacterController"/> (order 100). Also pushes once from <c>Start</c> so parameters
    /// match the motor before the first Animator update (Animator runs after <c>Update</c>, before <c>LateUpdate</c>).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CCS_AnimatorDriver : MonoBehaviour
    {
        private static readonly int InputMagnitudeId = Animator.StringToHash("InputMagnitude");
        private static readonly int InputHorizontalId = Animator.StringToHash("InputHorizontal");
        private static readonly int InputVerticalId = Animator.StringToHash("InputVertical");
        private static readonly int VerticalVelocityId = Animator.StringToHash("VerticalVelocity");
        private static readonly int RotationMagnitudeId = Animator.StringToHash("RotationMagnitude");
        private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
        private static readonly int IsSprintingId = Animator.StringToHash("IsSprinting");
        private static readonly int IsStrafingId = Animator.StringToHash("IsStrafing");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        [Header("References")]
        [Tooltip("Character that owns movement, input, and motor state.")]
        [SerializeField]
        private CCS_CharacterController characterController;

        [Tooltip("Animator on the character visual (locomotion controller asset).")]
        [SerializeField]
        private Animator locomotionAnimator;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CCS_CharacterController>();
            }

            if (locomotionAnimator == null && characterController != null)
            {
                locomotionAnimator = characterController.LocomotionAnimator;
            }
        }

        private void OnEnable()
        {
            EnsureRootMotionOff();
        }

        private void Start()
        {
            EnsureRootMotionOff();
            PushLocomotionParametersToAnimator();
        }

        /// <summary>
        /// LateUpdate runs after <see cref="CCS_CharacterController"/> (order 100) so motor and input match this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (characterController == null || locomotionAnimator == null)
            {
                return;
            }

            EnsureRootMotionOff();
            PushLocomotionParametersToAnimator();
        }

        /// <summary>
        /// Writes CCS locomotion parameters from <see cref="CCS_CharacterController"/> into the Animator.
        /// </summary>
        private void PushLocomotionParametersToAnimator()
        {
            if (characterController == null || locomotionAnimator == null)
            {
                return;
            }

            // Match CCS_CharacterController dead zone so idle/locomotion aligns with movement authority.
            Vector2 input = characterController.IsMoving ? characterController.LocomotionMoveInput : Vector2.zero;
            locomotionAnimator.SetFloat(InputMagnitudeId, Mathf.Clamp01(input.magnitude));
            locomotionAnimator.SetFloat(InputHorizontalId, input.x);
            locomotionAnimator.SetFloat(InputVerticalId, input.y);
            locomotionAnimator.SetFloat(VerticalVelocityId, characterController.LocomotionVerticalVelocity);
            locomotionAnimator.SetFloat(
                RotationMagnitudeId,
                Mathf.Abs(characterController.LocomotionYawVelocityDegreesPerSecond));
            locomotionAnimator.SetBool(IsGroundedId, characterController.IsMotorGrounded);
            locomotionAnimator.SetBool(IsSprintingId, false);
            locomotionAnimator.SetBool(IsStrafingId, false);

            if (characterController.ConsumeJumpAnimatorTrigger())
            {
                locomotionAnimator.SetTrigger(JumpId);
            }
        }

        private void EnsureRootMotionOff()
        {
            if (locomotionAnimator != null)
            {
                locomotionAnimator.applyRootMotion = false;
            }
        }
    }
}
