using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.CrossPlatformInput;

// I really should've made a copy instead of changing a thing from the standard assets directly.
// And it needs some serious refactoring.
namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class RigidbodyFirstPersonController : MonoBehaviour
    {
        private enum State
        {
            Grounded,
            Airborne,
            OnWall
        }

        [Serializable]
        public class MovementSettings
        {
            public float forwardSpeed  = 8.0f;  // Speed when walking forward
            public float backwardSpeed = 4.0f;  // Speed when walking backwards
            public float strafeSpeed   = 4.0f;  // Speed when walking sideways
            public float runMultiplier = 2.0f;  // Speed when sprinting
            public KeyCode runKey = KeyCode.LeftShift;
            public float jumpForce = 75f;
            public float wallJumpUpwardsModifier = 2f;
            public float wallJumpSidewaysModifier = 1f;
            public float wallJumpMinAwayFromWallForce = 10f;
            public float wallJumpDefaultAwayFromWallForce = 75f;
            public float timeToFallOffWall = 1f;
            public AnimationCurve SlopeCurveModifier = new AnimationCurve(new Keyframe(-90.0f, 1.0f), new Keyframe(0.0f, 1.0f), new Keyframe(90.0f, 0.0f));
            [HideInInspector] public float currentTargetSpeed = 8f;
#if !MOBILE_INPUT
            private bool m_Running;
#endif
            public void UpdateDesiredTargetSpeed(Vector2 input)
            {
                if (input == Vector2.zero) return;
                if (input.x > 0 || input.x < 0)
                {
                    currentTargetSpeed = strafeSpeed;
                }
                if (input.y < 0)
                {
                    currentTargetSpeed = backwardSpeed;
                }
                if (input.y > 0)
                {
                    //handled last as if strafing and moving forward at the same time forwards speed should take precedence
                    currentTargetSpeed = forwardSpeed;
                }
#if !MOBILE_INPUT
                if (Input.GetKey(runKey))
                {
                    currentTargetSpeed *= runMultiplier;
                    m_Running = true;
                }
                else
                {
                    m_Running = false;
                }
#endif
            }

#if !MOBILE_INPUT
            public bool Running
            {
                get { return m_Running; }
            }
#endif
        }

        [Serializable]
        public class AdvancedSettings
        {
            public float groundCheckDistance = 0.01f; // distance for checking if the controller is grounded ( 0.01f seems to work best for this )
            public float stickToGroundHelperDistance = 0.5f; // stops the character

            public float wallCheckDistance = 0.01f;
            public float stickToWallHelperDistance = 0.5f;
            public float stickToWallHelperForce = 1f;
            public float wallReattachmentTimeout = 0.05f;

            public float onSurfaceRigidbodyDrag = 5f;

            public float slowDownRate = 20f; // rate at which the controller comes to a stop when there is no input
            public bool airControl;
            [Tooltip("When in the air, should it change its velocity to move where the camera is looking?")]
            public bool alwaysForwardWhenAirborne;
            [Tooltip("When in the air, input will be scaled by this number. Set it to a smaller value to have less contol when in the air.")]
            public float airControlMultiplier = 1f;
            [Tooltip("Set it to 0.1 or more if you get stuck in walls")]
            public float shellOffset; //reduce the radius by that ratio to avoid getting stuck in wall (a value of 0.1f is nice)
        }

        [Serializable]
        public class AudioSettings
        {
            public PlayerAudio playerAudio;

            public float stepInterval = 0.5f;
            [Range(0f, 3f)] public float runStepSpeedup = 0.722f;
        }

        public Transform cameraTransform;
        public MovementSettings movementSettings = new MovementSettings();
        public MouseLook mouseLook = new MouseLook();
        public AdvancedSettings advancedSettings = new AdvancedSettings();
        [SerializeField] AudioSettings audioSettings = new AudioSettings();
        public LayerMask groundAndWallDetectionLayerMask = Physics.DefaultRaycastLayers;

        [SerializeField] [Range(0f, 180f)] float maxAwayFromWallInputAngle = 45f;
        [SerializeField] [Range(0f, 90f )] float awayFromWallLeanAngle = 10f;
        [SerializeField] float awayFromWallLeanAngleChangePerSecond = 180f;

        private Rigidbody m_RigidBody;
        private CapsuleCollider m_Capsule;
        private Vector3 m_SurfaceContactNormal;
        private bool m_Jump, m_Jumping;
        private float m_defaultDrag;

        private State m_State;
        private State m_PreviousState;

        private float m_StepCycle;

        private float wantedToFallOffWallTime;
        private float timeTillCanReattachToWall;

        public Vector3 velocity
        {
            get { return m_RigidBody.velocity; }
        }

        public bool isGrounded
        {
            get { return m_State == State.Grounded; }
        }

        public bool isAirborne
        {
            get { return m_State == State.Airborne; }
        }

        public bool isOnWall
        {
            get { return m_State == State.OnWall; }
        }

        public bool isJumping
        {
            get { return m_Jumping; }
        }

        public bool isRunning
        {
            get
            {
#if !MOBILE_INPUT
                return movementSettings.Running;
#else
	            return false;
#endif
            }
        }

        void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            m_Capsule = GetComponent<CapsuleCollider>();
            mouseLook.Init(transform, cameraTransform);

            m_defaultDrag = m_RigidBody.drag;

            Assert.IsNotNull(audioSettings.playerAudio);
        }

        void Update()
        {
            RotateView();

            if (!m_Jump && CrossPlatformInputManager.GetButtonDown("Jump"))
            {
                m_Jump = true;
            }
        }

        void FixedUpdate()
        {
            StateCheck();

            if (m_PreviousState == State.Airborne && m_State != State.Airborne)
            {
                m_Jumping = false;
                audioSettings.playerAudio.PlayLand();
            }

            if (m_PreviousState == State.OnWall && m_State != State.OnWall)
            {
                timeTillCanReattachToWall = advancedSettings.wallReattachmentTimeout;
            }

            Vector2 input = GetInput();
            movementSettings.UpdateDesiredTargetSpeed(input);

            // Handle movement
            if (IsNonZero(input))
            {
                // Always move along the camera forward as it is the direction that it being aimed at
                Vector3 desiredMove = cameraTransform.forward * input.y + cameraTransform.right * input.x;

                if (m_State == State.Grounded)
                {
                    desiredMove = Vector3.ProjectOnPlane(desiredMove, m_SurfaceContactNormal).normalized;
                    desiredMove *= movementSettings.currentTargetSpeed;

                    // TODO this speed limiting thing should be in other states as well (to some degree)
                    float targetSpeed = movementSettings.currentTargetSpeed;
                    if (m_RigidBody.velocity.sqrMagnitude < targetSpeed * targetSpeed)
                    {
                        m_RigidBody.AddForce(desiredMove * SlopeMultiplier(), ForceMode.Impulse);
                    }
                }
                else if (m_State == State.OnWall)
                {
                    if (!m_Jump)
                    {
                        desiredMove = Vector3.ProjectOnPlane(desiredMove, Vector3.up).normalized;
                        desiredMove *= movementSettings.currentTargetSpeed;

                        m_RigidBody.AddForce(desiredMove, ForceMode.Impulse);
                    }

                    m_RigidBody.AddForce(-Physics.gravity, ForceMode.Acceleration);
                }
                else if (m_State == State.Airborne && advancedSettings.airControl)
                {
                    desiredMove = Vector3.ProjectOnPlane(desiredMove, Vector3.up).normalized;
                    desiredMove *= movementSettings.forwardSpeed * advancedSettings.airControlMultiplier;

                    m_RigidBody.AddForce(desiredMove, ForceMode.Impulse);
                }
            }

            // Handle jumps

            if (m_State == State.Grounded)
            { 
                m_RigidBody.drag = advancedSettings.onSurfaceRigidbodyDrag;

                if (m_Jump)
                {
                    m_RigidBody.drag = m_defaultDrag;
                    m_RigidBody.velocity = new Vector3(m_RigidBody.velocity.x, 0f, m_RigidBody.velocity.z);
                    m_RigidBody.AddForce(new Vector3(0f, movementSettings.jumpForce, 0f), ForceMode.Impulse);

                    m_Jumping = true;
                    audioSettings.playerAudio.PlayJump();
                }

                if (!m_Jumping && IsZero(input) && m_RigidBody.velocity.magnitude < 1f)
                {
                    m_RigidBody.Sleep();
                }
            }
            else if (m_State == State.OnWall)
            {
                m_RigidBody.drag = advancedSettings.onSurfaceRigidbodyDrag;

                Vector3 awayFromWall = m_SurfaceContactNormal;
                Vector3 desiredMove = cameraTransform.forward * input.y + cameraTransform.right * input.x;
                desiredMove = Vector3.ProjectOnPlane(desiredMove, Vector3.up);      
                
                if (m_Jump)
                {
                    m_RigidBody.velocity = new Vector3(m_RigidBody.velocity.x, 0f, m_RigidBody.velocity.z);
                    
                    Vector3 force = (
                        Vector3.up * movementSettings.wallJumpUpwardsModifier +
                        desiredMove.normalized * movementSettings.wallJumpSidewaysModifier          
                    ) * movementSettings.jumpForce;
                    force += awayFromWall * movementSettings.wallJumpDefaultAwayFromWallForce;
                    
                    float awayFromWallComponent = Vector3.Dot(force, awayFromWall);
                    if (awayFromWallComponent < movementSettings.wallJumpMinAwayFromWallForce)
                    {
                        force += awayFromWall * (-awayFromWallComponent + movementSettings.wallJumpMinAwayFromWallForce);
                    }

                    m_RigidBody.drag = m_defaultDrag;
                    m_RigidBody.AddForce(force, ForceMode.Impulse);
                    m_Jumping = true;
                    
                    timeTillCanReattachToWall = advancedSettings.wallReattachmentTimeout;

                    audioSettings.playerAudio.PlayJump();
                }
                else
                {
                    bool wantsToFallOffWall = IsNonZero(desiredMove) && Vector3.Angle(awayFromWall, desiredMove) < maxAwayFromWallInputAngle;
                    if (wantsToFallOffWall)
                    {
                        wantedToFallOffWallTime += Time.fixedDeltaTime;
                    }
                    else
                    {
                        wantedToFallOffWallTime = 0f;
                    }

                    if (wantsToFallOffWall && wantedToFallOffWallTime >= movementSettings.timeToFallOffWall)
                    {
                        wantedToFallOffWallTime = 0f;
                        
                        timeTillCanReattachToWall = advancedSettings.wallReattachmentTimeout;
                    }
                    else
                    {
                        StickToWallHelper();
                    }
                }
            }
            else if (m_State == State.Airborne)
            {
                m_RigidBody.drag = m_defaultDrag;

                if (!m_Jumping)
                {
                    if (m_PreviousState == State.Grounded)
                    {
                        StickToGroundHelper();
                    }
                }
            }

            if (!isAirborne && IsNonZero(velocity) && IsNonZero(input))
            {
                ProgressStepCycle();
            }

            if (timeTillCanReattachToWall >= 0f)
            {
                timeTillCanReattachToWall -= Time.fixedDeltaTime;
            }

            m_Jump = false;
        }

        private float SlopeMultiplier()
        {
            float angle = Vector3.Angle(m_SurfaceContactNormal, Vector3.up);
            return movementSettings.SlopeCurveModifier.Evaluate(angle);
        }

        private void StickToGroundHelper()
        {
            RaycastHit hitInfo;
            if (Physics.SphereCast(transform.position, m_Capsule.radius * (1.0f - advancedSettings.shellOffset), Vector3.down, out hitInfo,
                                   ((m_Capsule.height / 2f) - m_Capsule.radius) + advancedSettings.stickToGroundHelperDistance, groundAndWallDetectionLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(Vector3.Angle(hitInfo.normal, Vector3.up)) < 85f)
                {
                    m_RigidBody.velocity = Vector3.ProjectOnPlane(m_RigidBody.velocity, hitInfo.normal);
                }
            }
        }

        private void StickToWallHelper()
        {
            RaycastHit hitInfo;
            if (RaycastWalls(advancedSettings.stickToWallHelperDistance, out hitInfo))
            {
                Vector3 awayFromWall = hitInfo.normal;
                m_RigidBody.velocity = Vector3.ProjectOnPlane(m_RigidBody.velocity, awayFromWall);
                m_RigidBody.AddForce(-awayFromWall * advancedSettings.stickToWallHelperForce, ForceMode.Impulse);
            }
        }

        private Vector2 GetInput()
        {
            return new Vector2(
                CrossPlatformInputManager.GetAxis("Horizontal"),
                CrossPlatformInputManager.GetAxis("Vertical")
            );
        }

        private void RotateView()
        {
            // avoids the mouse looking if the game is effectively paused
            if (Mathf.Abs(Time.timeScale) < float.Epsilon) return;

            Quaternion leanOffset;
            if (m_State == State.OnWall)
            {
                Vector3 wallNormal = m_SurfaceContactNormal;
                Vector3 wallTangent = Vector3.Cross(wallNormal, Vector3.up);

                leanOffset = Quaternion.Euler(
                    awayFromWallLeanAngle * Vector3.Dot(cameraTransform.forward, wallNormal), 
                    0f, 
                    -awayFromWallLeanAngle * Vector3.Dot(cameraTransform.forward, wallTangent)
                );
            }
            else
            {
                leanOffset = Quaternion.identity;
            }
            mouseLook.cameraRotationOffset = Quaternion.RotateTowards(
                mouseLook.cameraRotationOffset, 
                leanOffset, 
                awayFromWallLeanAngleChangePerSecond * Time.deltaTime
            );

            // get the rotation before it's changed
            float oldYRotation = transform.eulerAngles.y;
            mouseLook.LookRotation(transform, cameraTransform);
            if (m_State != State.Airborne || advancedSettings.alwaysForwardWhenAirborne)
            {
                // Rotate the rigidbody velocity to match the new direction that the character is looking
                Quaternion velRotation = Quaternion.AngleAxis(transform.eulerAngles.y - oldYRotation, Vector3.up);
                m_RigidBody.velocity = velRotation * m_RigidBody.velocity;
            }
        }

        private void StateCheck()
        {
            m_PreviousState = m_State;

            GroundCheck();
            if (m_State == State.Airborne && timeTillCanReattachToWall <= 0f)
            {
                WallCheck();
            }
        }

        /// sphere cast down just beyond the bottom of the capsule to see if the capsule is colliding round the bottom
        private void GroundCheck()
        {
            RaycastHit hitInfo;
            if (Physics.SphereCast(transform.position, m_Capsule.radius * (1.0f - advancedSettings.shellOffset), Vector3.down, out hitInfo,
                                   ((m_Capsule.height / 2f) - m_Capsule.radius) + advancedSettings.groundCheckDistance, groundAndWallDetectionLayerMask, QueryTriggerInteraction.Ignore))
            {
                m_State = State.Grounded;
                m_SurfaceContactNormal = hitInfo.normal;
            }
            else
            {
                m_State = State.Airborne;
                m_SurfaceContactNormal = Vector3.up;
            }
        }

        private void WallCheck()
        {
            RaycastHit hitInfo;
            if (RaycastWalls(advancedSettings.wallCheckDistance, out hitInfo))
            {
                //Debug.Log("Went to State.OnWall because of " + hitInfo.collider.gameObject);
                m_State = State.OnWall;
                m_SurfaceContactNormal = hitInfo.normal;
            }
            else
            {
                m_State = State.Airborne;
                m_SurfaceContactNormal = Vector3.up;
            }
        }

        private bool RaycastWalls(float maxDistance, out RaycastHit hitInfo)
        {
            Vector3 position = transform.position;

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            if (CastAgainstWall(position, right, maxDistance, out hitInfo)) return true;
            if (CastAgainstWall(position, -right, maxDistance, out hitInfo)) return true;
            if (CastAgainstWall(position, forward, maxDistance, out hitInfo)) return true;
            if (CastAgainstWall(position, -forward, maxDistance, out hitInfo)) return true;

            return false;
        }

        private bool CastAgainstWall(Vector3 position, Vector3 direction, float maxDistance, out RaycastHit hitInfo)
        {
            return Physics.SphereCast(
                new Ray(position, direction),
                radius: m_Capsule.radius * (1.0f - advancedSettings.shellOffset),
                hitInfo: out hitInfo,
                maxDistance: maxDistance,
                layerMask: groundAndWallDetectionLayerMask,
                queryTriggerInteraction: QueryTriggerInteraction.Ignore
            );
        }

        private void ProgressStepCycle()
        {
            float speed = velocity.magnitude;
            if (isRunning) speed *= audioSettings.runStepSpeedup;
            m_StepCycle += speed * Time.fixedDeltaTime;

            if (m_StepCycle > audioSettings.stepInterval)
            {
                do m_StepCycle -= audioSettings.stepInterval;
                while (m_StepCycle > audioSettings.stepInterval);

                audioSettings.playerAudio.PlayFootstep();
            }
        }

        private static bool IsNonZero(Vector2 vector)
        {
            return Mathf.Abs(vector.x) > float.Epsilon || Mathf.Abs(vector.y) > float.Epsilon;
        }

        private static bool IsZero(Vector2 vector)
        {
            return Mathf.Abs(vector.x) <= float.Epsilon && Mathf.Abs(vector.y) <= float.Epsilon;
        }
    }
}
