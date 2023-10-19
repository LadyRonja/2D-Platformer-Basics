using System;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Developed by Ronja Mattsson
/// git: https://github.com/LadyRonja
/// linkedin: https://www.linkedin.com/in/ronja-mattsson-922943205/
/// itch.io: https://ladyronja.itch.io/
/// </summary>

/// TODO:
/// Stop sliding on slopes
public enum PlayerState
{
    Idle,
    Walk,
    Run,
    Crouch,
    Jump,
    Fall,
    Dead,
    Attack,
    Dash
}
public class PlayerMovement : MonoBehaviour
{
    [Header("Horizontal movement")]
    public bool useAcceleration = false;
    [Tooltip("Increase current speed to apply to player by X/Second")] public float acceleration = 20f;
    [Tooltip("Reduce current speed to apply to player by X/Second")] public float deacceleration = 20f;
    [Space(25)]
    public float groundSpeedMax = 20f;
    public float airSpeedMax = 20f;
    [Range(0.7f, 0.999f)] public float groundedDeceleration = 0.7f;
    public bool onlyDecelerateInAirWithoutInput = true;
    [Range(0.7f, 0.999f)] public float airDeceleration = 0.975f;

    [Header("Jumping controls")]
    public float jumpForce = 30f;
    [Tooltip("You can always jump off the ground, doubleJumps only count from the air, exluding coyote time")] 
    public int amountOfDoubleJumps = 1;
    public bool useCoyoteTime = true;
    public float coyoteTime = 0.3f;
    [Space(25)] 
    [Tooltip("Different gravity for jumping and falling?")]
    public bool increasedFallGravity = true;
    public float jumpGravity = 4f;
    public float fallGravity = 8f;
    [Space(25)]
    public bool holdToJumpHigher = false;
    [Tooltip("Letting go of the jump button reduces upward velocity by this %")]
    [Range(0f, 1f)] public float shortenJumpPercentage = 0.2f;
    [Space(25)]
    public bool bufferJumpInput = true;
    [Range(0, 10)] public int inputBufferAmount = 2;
    [Space(25)]
    public bool useCorenerCorrection = true;
    [Range(0.1f, 0.2f)] public float cornerCorrectionLeniancy = 0.15f;

    // Essentials
    Vector2 directionalInput = Vector2.zero;
    Rigidbody2D rb;
    Collider2D col;

    float currentSpeedGround = 0f;
    float currentSpeedAir = 0f;
    float coyoteTimer = 0;
    bool grounded = false;
    bool jumpIsBuffered = false;
    int framesLeftForBufferedJump = 0;
    int jumpsUsed = 0;

    // Animations
    PlayerState state = PlayerState.Idle;
    PlayerState lastState = PlayerState.Idle;
    bool determineFlipOnInput = true;
    bool movingLeft = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    #region Various Update functions
    private void Update()
    {
        GroundCheck();
        HorizontalMovementManager();
        JumpManager();
        GravityAdjustment();
        CornerCorrecting();
        DetermineState();
    }

    private void FixedUpdate()
    {
        FrictionManager();
    }

    private void LateUpdate()
    {
        // Inform animator of flip here
        // example: animationHandler.SetFlip(movingLeft);

        if (lastState != state)
        {
            // Call animation handler from here
            // example: animationHandler.UpdateState(state);
        }
        lastState = state;
    }
    #endregion

    private void GroundCheck()
    {
        grounded = false;
        RaycastHit2D hit;
        hit = Physics2D.Raycast(this.transform.position, Vector2.down, col.bounds.size.y + 0.1f);

        if (hit)
        {
            grounded = true;
            coyoteTimer = coyoteTime;
            jumpsUsed = 0;
        }
    }

    private void HorizontalMovementManager()
    {
        directionalInput.x = Input.GetAxisRaw("Horizontal");

        // Apply movement
        if(directionalInput.x != 0)
        {

            // Determine speed
            if (useAcceleration)
            {
                currentSpeedGround += acceleration * Time.deltaTime;
                currentSpeedAir += acceleration * Time.deltaTime;
            }
            else
            {
                currentSpeedGround = groundSpeedMax;
                currentSpeedAir = airSpeedMax;
            }

            ClampCurrentSpeedValue();

            float speedToApply = grounded ? currentSpeedGround : currentSpeedAir;

            rb.velocity = new Vector2(directionalInput.x * speedToApply, rb.velocity.y);
        }
        else
        {
            // If not using any input, reduce speed over time
            currentSpeedGround -= Time.deltaTime * deacceleration;
            currentSpeedAir -= Time.deltaTime * deacceleration;

            ClampCurrentSpeedValue();
        }

        void ClampCurrentSpeedValue()
        {
            currentSpeedAir = Mathf.Clamp(currentSpeedAir, 0, airSpeedMax);
            currentSpeedGround = Mathf.Clamp(currentSpeedGround, 0, groundSpeedMax);
        }
    }

    #region Jumping
    private void JumpManager()
    {
        coyoteTimer -= Time.deltaTime;

        if (bufferJumpInput)
            ManageJumpingWithBufferTime();
        else
            ManageJumpingWithoutBuffering();

        // Shorten jump if you let go of jump button
        if (holdToJumpHigher)
        {
            if (Input.GetKeyUp(KeyCode.Space) && rb.velocity.y > 0)
            {
                rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * shortenJumpPercentage);
            }
        }
    }

    private void ManageJumpingWithBufferTime()
    {
        if (jumpIsBuffered)
            framesLeftForBufferedJump--;

        if (framesLeftForBufferedJump <= 0)
            jumpIsBuffered = false;
    
        // Get Jump Input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpIsBuffered = true;
            framesLeftForBufferedJump = inputBufferAmount;
        }

        // Jump
        if (jumpIsBuffered)
        {
            if (grounded)
            {
                Jump(false);
            }
            else if (useCoyoteTime && coyoteTimer > 0)
            {
                Jump(false);
            }
            else if (jumpsUsed < amountOfDoubleJumps)
            {
                Jump(true);
            }
        }  
    }

    private void ManageJumpingWithoutBuffering()
    {
        // Jumping
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // If using cayote time, see if time has ran out
            bool coyoteAllow = false;
            if (useCoyoteTime && coyoteTimer > 0)
                coyoteAllow = true;

            if (grounded || coyoteAllow)
            {
                Jump(false);
            }
            else if (jumpsUsed < amountOfDoubleJumps)
            {
                Jump(true);
            }
        }
    }

    /// <summary>
    /// Requires boolean input to avoid player walking of cliff and having too many jumps left
    /// </summary>
    /// <param name="isDoubleJump"></param>
    private void Jump(bool isDoubleJump)
    {
        jumpIsBuffered = false;
        coyoteTimer = 0;
        if(isDoubleJump)
           jumpsUsed++;

        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }
    #endregion

    private void GravityAdjustment()
    {
        // Go faster down than up
        if (rb.velocity.y < 0)
            rb.gravityScale = fallGravity;
        else
            rb.gravityScale = jumpGravity;
    }

    /// <summary>
    /// Colliding at very low speeds will look jerky with this solution
    /// Potential fixes: move graphics seperetly, calculate speed needed to move, or any other solution that doesn't teleport the player
    /// </summary>
    private void CornerCorrecting()
    {
        if (!useCorenerCorrection) 
            return;

        if (grounded) 
            return;

        RaycastHit2D airCollisionLower;
        RaycastHit2D airCollisionUpper;

        Vector3 startPointLower = transform.position;
        Vector3 startPointUpper = transform.position;

        startPointLower.y -= col.bounds.size.y / 2;
        startPointUpper.y -= (col.bounds.size.y / 2) - cornerCorrectionLeniancy;

        Vector2 directionToCheck = Vector2.zero;

        if (directionalInput.x < 0)
            directionToCheck = Vector2.left;
        else if (directionalInput.x > 0)
            directionToCheck = Vector2.right;
        else if (rb.velocity.x < 0)
            directionToCheck = Vector2.left;
        else if (rb.velocity.x > 0)
            directionToCheck = Vector2.right;
        else
            return;

        airCollisionLower = Physics2D.Raycast(startPointLower, directionToCheck, col.bounds.size.x);
        airCollisionUpper = Physics2D.Raycast(startPointUpper, directionToCheck, col.bounds.size.x);

        if(airCollisionLower && !airCollisionUpper)
        {
            rb.velocity = Vector2.zero;
            Vector3 targetPosition = rb.transform.position;
            targetPosition.x += /*col.bounds.size.x */ directionToCheck.x * airCollisionLower.distance;
            targetPosition.y += startPointUpper.y - startPointLower.y;
            rb.transform.position = targetPosition;
        }
        else if (airCollisionLower)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }

    private void FrictionManager()
    {
        // Player has no friction, slow down over time - but not while turning
        if (grounded)
        {
            if (directionalInput.x == 0)
            {
                if (Mathf.Abs(directionalInput.x) == 0 || (directionalInput.x < 0 == rb.velocity.x > 0))
                    Decelerate(groundedDeceleration);
            }
        }
        else
        {
            if (onlyDecelerateInAirWithoutInput && directionalInput.x == 0)
            {
                Decelerate(airDeceleration);
            }
            else if (!onlyDecelerateInAirWithoutInput)
            {
                Decelerate(airDeceleration);
            }
        }

        void Decelerate(float decelerationAmount)
        {
            decelerationAmount = Mathf.Clamp(decelerationAmount, 0, 1);
            rb.velocity = new Vector2(rb.velocity.x * decelerationAmount, rb.velocity.y);
        }
    }

    /// <summary>
    /// Primarily used for animations.
    /// </summary>
    private void DetermineState()
    {
        // Jumping, Falling
        if (!grounded && rb.velocity.y > 0)
            state = PlayerState.Jump;
        else if (!grounded && rb.velocity.y < 0)
            state = PlayerState.Fall;

        // Idle, Walking, Running
        if (directionalInput.x == 0 /*&& Mathf.Abs(rb.velocity.x) <= 0.05f */ && grounded) // Uncomment to avoid "idle slide"
            state = PlayerState.Idle;
        else if (grounded/* && Mathf.Abs(rb.velocity.x) > runningThresehold*/) // Uncomment and define runningThresehold if using both running/walking animations
            state = PlayerState.Run;
        else if (grounded)
            state = PlayerState.Walk;

        // Determine Flip
        // Ternary conditional operator:
        // "condition ? (return if true) : (return if false)"
        float determiningVariable = determineFlipOnInput ? directionalInput.x : rb.velocity.x;

        if (determiningVariable > 0)
            movingLeft = false;
        else if(determiningVariable < 0) 
            movingLeft = true;
    }
}
