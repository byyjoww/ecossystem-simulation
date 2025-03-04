using System;
using UnityEngine;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof (CharacterController))]
    [RequireComponent(typeof (AudioSource))]

    public class MeshFirstPersonController : MonoBehaviour
    {
        [SerializeField] private bool m_IsWalking = true;
        [SerializeField] private float m_WalkSpeed = 5f;
        [SerializeField] private float m_RunSpeed = 10f;
        [SerializeField] [Range(0f, 1f)] private float m_RunstepLenghten = 0.7f;
        [SerializeField] private float m_JumpSpeed = 10f;
        [SerializeField] private float m_StickToGroundForce = 10f;
        [SerializeField] private float m_GravityMultiplier = 2f;
        [SerializeField] private MouseLook m_MouseLook;
        [SerializeField] private bool m_UseFovKick = true;
        [SerializeField] private FOVKick m_FovKick = new FOVKick();
        [SerializeField] private bool m_UseHeadBob = true;
        [SerializeField] private CurveControlledBob m_HeadBob = new CurveControlledBob();
        [SerializeField] private LerpControlledBob m_JumpBob = new LerpControlledBob();
        [SerializeField] private float m_StepInterval = 5;
        [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
        [SerializeField] private AudioClip m_JumpSound;           // the sound played when character leaves the ground.
        [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.

        [Header("Input Events")]
        [SerializeField] private FloatScriptableEvent OnVerticalInput;
        [SerializeField] private FloatScriptableEvent OnHorizontalInput;
        [SerializeField] private BoolScriptableEvent OnJumpInput;
        [SerializeField] private BoolScriptableEvent OnSprintInput;
        [SerializeField] private BoolScriptableEvent OnShoot;

        private Camera m_Camera;
        private bool m_Jump;
        private float m_YRotation;
        private Vector2 Input 
        { 
            get 
            {
                var input = new Vector2(m_horizontal, m_vertical);

                if (input.sqrMagnitude > 1)
                {
                    input.Normalize();
                }

                return input;
            } 
        }
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;
        private float m_StepCycle;
        private float m_NextStep;
        private bool m_Jumping;
        private AudioSource m_AudioSource;
        private float m_horizontal;
        private float m_vertical;
        private float Speed => m_IsWalking ? m_WalkSpeed : m_RunSpeed;
        public bool isShooting;

        private Animator anim;
        public string animationClip;

        // Use this for initialization
        private void Start()
        {
            anim = GetComponent<Animator>();

            ChangeAnimation("isIdle");
            
            m_CharacterController = GetComponent<CharacterController>();
            m_Camera = Camera.main;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;
            m_FovKick.Setup(m_Camera);
            m_HeadBob.Setup(m_Camera, m_StepInterval);
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle/2f;
            m_Jumping = false;
            m_AudioSource = GetComponent<AudioSource>();
			m_MouseLook.Init(transform , m_Camera.transform);

            OnShoot.OnRaise += Shoot;
            OnJumpInput.OnRaise += SetJump;
            OnHorizontalInput.OnRaise += SetHorizontal;
            OnVerticalInput.OnRaise += SetVertical;
            OnSprintInput.OnRaise += SetSprint;
        }

        private void ChangeAnimation(string animation)
        {
            Debug.Log("Changing animation to " + animation);
            anim.ResetTrigger(animationClip);
            animationClip = animation;
            anim.SetTrigger(animation);
        }

        #region JUMP
        private void Jump()
        {
            ChangeAnimation("isIdle");
            m_MoveDir.y = m_JumpSpeed;
            PlayJumpSound();            
            m_Jump = false;
            m_Jumping = true;
        }

        private void Land()
        {
            StartCoroutine(m_JumpBob.DoBobCycle());
            PlayLandingSound();
            m_MoveDir.y = 0f;
            m_Jumping = false;
        }

        private void PlayJumpSound()
        {
            m_AudioSource.clip = m_JumpSound;
            m_AudioSource.Play();
        }

        private void PlayLandingSound()
        {
            m_AudioSource.clip = m_LandSound;
            m_AudioSource.Play();
            m_NextStep = m_StepCycle + .5f;
        }

        private void CheckJumpStatus()
        {
            // If character just landed
            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                Land();
            }

            // If character isn't jumping
            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
            {
                m_MoveDir.y = 0f;
            }

            m_PreviouslyGrounded = m_CharacterController.isGrounded;
        }

        private void CheckForJump()
        {
            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_Jump)
                {
                    Jump();
                }
            }
            else
            {
                m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
            }
        }
        #endregion

        #region MOVEMENT
        private void UpdateMovementDirection()
        {
            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 desiredMove = transform.forward * Input.y + transform.right * Input.x;

            // get a normal for the surface that is being touched to move along it
            RaycastHit hitInfo;
            Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down, out hitInfo,
                               m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

            m_MoveDir.x = desiredMove.x * Speed;
            m_MoveDir.z = desiredMove.z * Speed;
        }

        private void ProgressStepCycle(float speed)
        {
            if (m_CharacterController.velocity.sqrMagnitude > 0 && (Input.x != 0 || Input.y != 0))
            {
                m_StepCycle += (m_CharacterController.velocity.magnitude + (speed * (m_IsWalking ? 1f : m_RunstepLenghten))) * Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep))
            {
                return;
            }

            m_NextStep = m_StepCycle + m_StepInterval;

            PlayFootStepAudio();
        }

        private void PlayFootStepAudio()
        {
            if (!m_CharacterController.isGrounded)
            {
                return;
            }
            // pick & play a random footstep sound from the array,
            // excluding sound at index 0
            int n = Random.Range(1, m_FootstepSounds.Length);
            m_AudioSource.clip = m_FootstepSounds[n];
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
            // move picked sound to index 0 so it's not picked next time
            m_FootstepSounds[n] = m_FootstepSounds[0];
            m_FootstepSounds[0] = m_AudioSource.clip;
        }
        #endregion

        #region ACTIONS
        private void Shoot(bool isShooting)
        {
            if (isShooting & !this.isShooting)
            {
                ChangeAnimation("isShooting");
                this.isShooting = true;
            }
            else
            {
                ChangeAnimation("isIdle");
                this.isShooting = false;
            }
        }
        #endregion

        private void Update()
        {
            RotateView();
            CheckJumpStatus();
        }

        private void FixedUpdate()
        {
            CheckFOVKick();
            UpdateMovementDirection();

            CheckForJump();

            if (!isShooting)
            {
                m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);
                ProgressStepCycle(Speed);
                UpdateCameraPosition(Speed);
            }
            else
            {
                m_CollisionFlags = m_CharacterController.Move(new Vector3(0,m_MoveDir.y, 0) * Time.fixedDeltaTime);
            }

            m_MouseLook.UpdateCursorLock();
        }

        private void UpdateCameraPosition(float speed)
        {
            Vector3 newCameraPosition;
            if (!m_UseHeadBob)
            {
                return;
            }
            if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
            {
                m_Camera.transform.localPosition =
                    m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude +
                                      (speed*(m_IsWalking ? 1f : m_RunstepLenghten)));
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_Camera.transform.localPosition.y - m_JumpBob.Offset();
            }
            else
            {
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
            }
            m_Camera.transform.localPosition = newCameraPosition;
        }

        #region INPUT
        void SetHorizontal(float h)
        {
           
        }

        void SetVertical(float v)
        {
            if (isShooting)
            {
                return;
            }

            m_vertical = v;

            if (v > 0 && m_IsWalking && !m_Jumping)
            {
                if (animationClip != "isWalking")
                {
                    ChangeAnimation("isWalking");

                    Debug.Log("changed to walk");
                }
            }
            else if (v > 0 && !m_IsWalking && !m_Jumping)
            {
                if (animationClip != "isRunning")
                {
                    ChangeAnimation("isRunning");

                    Debug.Log("changed to run");
                }
            }
            else if (v == 0 && !m_Jumping)
            {
                if (animationClip != "isIdle")
                {
                    ChangeAnimation("isIdle");

                    Debug.Log("changed to idle");
                }
            }
            
        }

        void SetJump(bool b)
        {
            m_Jump = b;
        }

        void SetSprint(bool b)
        {
            m_IsWalking = !b;
        }

        private void CheckFOVKick()
        {
            bool waswalking = m_IsWalking;

            // handle speed change to give an fov kick only if the player is going to a run, is running and the fovkick is to be used
            if (m_IsWalking != waswalking && m_UseFovKick && m_CharacterController.velocity.sqrMagnitude > 0)
            {
                StopAllCoroutines();
                StartCoroutine(!m_IsWalking ? m_FovKick.FOVKickUp() : m_FovKick.FOVKickDown());
            }
        }

        private void RotateView()
        {
            m_MouseLook.LookRotation(transform, m_Camera.transform);
        }
        #endregion

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            //dont move the rigidbody if the character is on top of it
            if (m_CollisionFlags == CollisionFlags.Below)
            {
                return;
            }

            if (body == null || body.isKinematic)
            {
                return;
            }
            body.AddForceAtPosition(m_CharacterController.velocity*0.1f, hit.point, ForceMode.Impulse);            
        }
    }
}
