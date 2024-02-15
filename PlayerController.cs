using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, IPlayer, IDamageable
{
    public delegate void PlayerDeath();
    public static event PlayerDeath OnPlayerDeath;

    //fields
    private Rigidbody2D rb;
    private Collider2D coll;
    public PlayerInput playerInput;

    #region HorizontalMovementFields
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 15.0f;        //player speed moving across the X axis
    [SerializeField]
    private float acceleration = 5.0f;      //player acceleration to acheive target speed
    [SerializeField]
    private float decceleration = 10.0f;    //player decceleration to slow down to target speed, or to slow down if they're going too fast
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float velControlDefault = 1.0f;   //the power of the acceleration and decceleration, or amount of control the player has: 1 is 100% of the power
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float aerialControl = 1.0f;     //power of acceleration and decceleration in the air, or amount of control the player has
    [SerializeField]
    private bool limitAerialTurning = true; //determines if the player changes the direction their facing on the ground & air, or only the ground
    private float dirX;                 //the axis input that determines if the player character moves left or right
    private bool facingRight;           //states where the player is facing
    private float targetVelocity;       //the speed and direction the tries to player moves at
    private float speedDif;             //the difference of speed between the current player speed and the target velocity
    private float velPower;             //holds the velocity power set by velPowerDefault and aerialControl
    private float accelRate;            //holds either the acceleration or decceleration value, depending on the speedDif
    private float movement;             //the movement that is added as a force for that frame
    #endregion

    //Vertical Movement Fields
    private float dirY;

    #region HorizontalJumpBoostFields
    [Header("Horizontal Jump Boost")]
    [SerializeField]
    private bool willJumpBoost = true;      //determines if the player will be boosted based on the direction that player inputs
    [SerializeField]
    private float jumpBoostMultiplier = 1;  //how much of the movespeed is added as an impulse to the player when jumping
    [SerializeField]
    private float jumpBoostCoyoteMultiplier = 1; //how much of the movespeed is added as an impulse to the player when jumping midair
    #endregion

    #region MovingOnPlatforms
    private bool isOnMovingPlatform = false;    //boolean for if the player is on a Moving Platform or not
    private MovingPlatform currMovPlatform;     //reference to the moving platform the player is on
    private float platFriction;
    [Header("Friction Materials")]
    [SerializeField]
    private PhysicsMaterial2D defaultMaterial;  //The default physics material on the player (should have a physics value of 0)
    [SerializeField]
    private PhysicsMaterial2D platformMaterial; //The physics material applied to the player once on a platform (should have a friction coefficient of 1)
    #endregion

    //Friction
    private float frictionAmount = 0.0f;    //current friction of the surface the player is on
    private float frictionApplied;          //the force that is applied to the player

    #region JumpFields
    //Jumping Stuff
    private float maxJumpHeight;    //how high the player can jump; set in the GameManager script
    public float playerJumpHeight   //property for the GameManager to set the jump height
    {
        get { return maxJumpHeight; }
        set { maxJumpHeight = value; }
    }
    private float jumpTimeLimit;    //how long the player's jump will be; set in the GameManager script
    public float playerJumpTime     //property for the GameManager to set the jump height
    {
        get { return jumpTimeLimit; }
        set { jumpTimeLimit = value; }
    }
    private Vector2 initialJumpVelocity;        //the force that the player jumps with; determined by gravity, time and final velocity at the precipice of the jump which is just 0.
    [Header("Jumping")]
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float jumpCutMultiplier;            //the amount that the jump is cut off by if the player prematurely releases the jump button: 1 is 100% of the rest of the jump is cut, 0 is 0%
    [SerializeField]
    private float coyoteTimeMax = 0.05f;        //amount of time player can fall off a platform/ground and still perform a jump
    [SerializeField]
    private float jumpBuffer = 0.2f;            //how much time the player has to press the jump button early
    [SerializeField]
    private bool cutMomentumInAir = false;      //determines if the player will stop momentum in the air after releasing a horizontal movement input
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float cutMomentumPower = 1.0f;      //how effective is the power of stopping the player momentum in air
    private float coyoteTimer;                  //keeps track of coyote time
    private float jumpBufferTimer;              //keeps track of jump buffer time
    private GroundedCheck groundCheck;          //A new class whose purpose is to check if the given player object is on the/a ground
    private bool isGrounded = true;             //player isGrounded
    private bool isJumping = false;             //player is holding/pressing the jump button and they get/have a positive Y velocity
    #endregion

    #region JumpingHang
    [Header("Jumping Hang")]
    [SerializeField]
    private bool jumpHang = false;          //determines if the player has extra hang time as they reach and fall from the precipice of their jump by reducing their gravity scale
    [SerializeField]
    private float jumpHangVelRange = 0;     //the absolute value of both ends of the velocity range where the player will experience a reduction of their gravity scale
    [SerializeField]
    private float jumpHangGravScale = 1;    //the gravity scale that the player is subject to at the precipice of their jump to hang
    #endregion

    #region GravityFields
    [Header("Gravity")]
    [SerializeField]
    private float fallSpeedClamp = 50.0f;       //the speed the player is clamped at when free-falling
    [SerializeField]
    private bool modifyFallGravity = false;     //choice if gravity is modified or not
    private float gravityScaleDefault;          //the default gravity scale for the player set by the rigidbody
    [SerializeField]
    private float fallGravityMultiplier = 1.0f; //How effective is gravity on the player when falling
    private Vector2 fallGravity;                //the new gravity that affects the player when falling
    #endregion

    private bool isCrouching = false;

    #region Grabbing/HoldingFields
    private GameObject holding = null;  //keeps track of what object the player grabbed and is currently holding
    private GameObject holdColliderObj;    //reference to the collider that gets turned on/off whether the player is holding an object or not
    private Collider2D holdColliderCol;

    #endregion

    private string slottedAnimusCrystal = "ice";

    //method that is called whenever the player lands on a moving platform; sets player friction coefficient to 1
    void IPlayer.OnFriction(GameObject platform)
    {
        isOnMovingPlatform = true;
        //coll.sharedMaterial = platformMaterial;             //set player collider's friction coefficient via a physics material change
        //holdColliderCol.sharedMaterial = platformMaterial;  //set the friction coefficient for the collider attributed to the player holding a block
        currMovPlatform = platform.GetComponent<MovingPlatform>();
    }
    
    //method that is called whenever the player leaves a moving platform; sets player friction coeffecient to 0
    void IPlayer.OffFriction()
    {
        isOnMovingPlatform = false;
        //coll.sharedMaterial = defaultMaterial;              //set player collider's friction coefficient via a physics material change
        //holdColliderCol.sharedMaterial = defaultMaterial;   //set the friction coefficient for the collider attributed to the player holding a block
        currMovPlatform = null;
    }

    void IPlayer.UpdateFriction(float f)
    {
        if (Mathf.Abs(dirX) < 0.01) rb.AddForce(Vector2.right * (-platFriction), ForceMode2D.Impulse);
        platFriction = f;
        Debug.Log("platFriction: " + platFriction);
    }

    void IDamageable.Die()
    {
        if (holding != null) grabDrop(); //drop the item if the player dies
        OnPlayerDeath();
    }

    private void OnEnable()
    {
        LandTrigger.OnLanding += GetGroundFriction; //set up the get friction method upon OnLanding trigger
    }

    private void OnDisable()
    {
        LandTrigger.OnLanding -= GetGroundFriction; //remove the get friction method
    }

    // Everything that needs to be set up prior to the player... playing
    void Start()
    {
        holdColliderObj = transform.Find("HoldingCollider").gameObject; //get the reference to the player holding collider
        holdColliderObj.SetActive(false);      //set the holdCollider to off, as the player shouldn't be holding something to start with
        holdColliderCol = holdColliderObj.GetComponent<Collider2D>();   //get reference to the collider in the HoldingCollider child object, this will allow changes to the physics material
        rb = GetComponent<Rigidbody2D>();   //set the rigidbody
        coll = GetComponent<Collider2D>();  //get the player collider
        playerInput = GetComponent<PlayerInput>();
        facingRight = true;
        dirX = 0.0f;
        dirY = 0.0f;

        //the velocity of the jump as determined by the gravity and jump time (and final velocity which is 0)
        initialJumpVelocity = Vector2.up * -1 * Physics2D.gravity.y * jumpTimeLimit;

        //multiply the gravity multiplier to the existing gravity value to create the modified gravity
        fallGravity = new Vector2(0.0f, Physics2D.gravity.y * fallGravityMultiplier);

        groundCheck = new GroundedCheck(gameObject, rb, 0.25f); //instantiate class GroundedCheck to be able to consistently check grounded status

        gravityScaleDefault = rb.gravityScale;  //set the default for the gravity scale to the player gravity scale set to the rigidbody

        isOnMovingPlatform = false;             
        coll.sharedMaterial = defaultMaterial;              //make sure the collision box's physics material is set up properly
        holdColliderCol.sharedMaterial = defaultMaterial;   //likewise for the child object, HoldingCollider. Make sure to set the collision box's physics material properly
        
        //debugs
        Debug.Log("initialJumpVelocity: " + initialJumpVelocity.y);
        Debug.Log("gravity: " + Physics2D.gravity.y);
        Debug.Log("modified gravity: " + fallGravity.y);
        Debug.Log("jumpTimeLimit: " + jumpTimeLimit);
        
        Debug.Log("Collision center: " + coll.bounds.center);
    }

    private void FixedUpdate()
    {
        #region HorizontalMovement
        //grounded effects for horizontal movement
        if (isGrounded || isOnMovingPlatform)
        {
            velPower = velControlDefault; //set velPower for acceleration to the default for the floor

            if (limitAerialTurning) Turn(); //using this section here makes it so the player only changes direction of facing when on ground, not in air; Only occurs if the aerial turning is limited

            //Friction
            if (isGrounded && Mathf.Abs(dirX) < 0.01 && !isOnMovingPlatform)
            {
                frictionApplied = Mathf.Min(Mathf.Abs(rb.velocity.x), Mathf.Abs(frictionAmount));   //determines what amount of force will be applied to the player's velocity to simulate friction
                frictionApplied *= Mathf.Sign(rb.velocity.x);                                       //direction of friction force
                rb.AddForce(Vector2.right * -frictionApplied, ForceMode2D.Impulse);                 //apply the friction force
            }
            else if (isOnMovingPlatform && Mathf.Abs(dirX) < 0.01)
            {
                frictionApplied = Mathf.Min(Mathf.Abs(rb.velocity.x - platFriction), Mathf.Abs(frictionAmount));
                frictionApplied *= Mathf.Sign(rb.velocity.x - platFriction);
                rb.AddForce(Vector2.right * -frictionApplied, ForceMode2D.Impulse);
            }

        }
        else velPower = aerialControl; //if not grounded, aerialControl is set as velPower's value

        if (!isGrounded && Mathf.Abs(dirX) <= 0.01f && cutMomentumInAir && !isOnMovingPlatform)    //if statement to check if the player's aerial momentum should be cut when no input is given.
        {
            rb.AddForce(Vector2.right * -rb.velocity.x * cutMomentumPower, ForceMode2D.Impulse); //cuts the player's horizontal momentum
        }
        else
        {
            //x-axis movement based on direction
            targetVelocity = isOnMovingPlatform ? (dirX * moveSpeed) + platFriction : dirX * moveSpeed;          //give direction to the speed to get the target velocity
            speedDif = targetVelocity - rb.velocity.x;  //the difference of speed between player speed and target velocity
            accelRate = (Mathf.Abs(targetVelocity) > 0.01f && Mathf.Abs(rb.velocity.x - platFriction) <= moveSpeed) ? acceleration + Mathf.Abs(platFriction) : decceleration + Mathf.Abs(platFriction);    //Determine if the player slows down or speeds up based on if
                                                                                                                                        //the target speed is greater than 0.01 and if the current
                                                                                                                                        //speed is below or matches the target
            //Dertermine actual movement force based on acceleration rate, speed difference to target velocity and its power.
            //Add the platform movespeed and the strength of the gravity as well if the player is on a platform to counteract the friction applied to the player.
            movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velPower) * Mathf.Sign(speedDif);
            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);   //apply the force
        }
        #endregion


        #region Jumping
        //below are all the statements to determine what happens while in the air and with jumping
        if (jumpBufferTimer > 0) isJumping = true; //if the player has presseed the jump input within the jumpBuffer time, player can jump

        if (isGrounded || isOnMovingPlatform) //below is everything that is checked when player is grounded on the floor or a moving platform
        {
            if (rb.gravityScale != gravityScaleDefault) rb.gravityScale = gravityScaleDefault; //reset the gravityScale to the default

            if (coyoteTimer < coyoteTimeMax) coyoteTimer = coyoteTimeMax; //coyote time reset to its max

            if (isJumping) Jump(); //player pressing jump button triggers the jump, and the negative velocity requirement ensures that multiple jump triggers
                                                            //don't occur with the grounded check collision box still overlapping with the ground at the beginning frames of the jump
        }
        else if (!isGrounded)
        {
            if (isCrouching) StopCrouching();                           //player stops crouching as they are not Grounded

            if (coyoteTimer > 0 && isJumping) Jump();                   //player pressing jump button and coyote timer still having time determines if the player can jump midair
            else if (isJumping && rb.velocity.y <= 0) isJumping = false;//if jump button is pressed, but player is just falling, set isJumping to false
            else if (!isJumping && rb.velocity.y > 0) rb.AddForce(Vector2.down * rb.velocity.y * jumpCutMultiplier, ForceMode2D.Impulse); //if player lets go of the jump button early in their jump,
                                                                                                                                            //they prematurely fall depending on the jumpCutMultiplier

            if (Mathf.Abs(rb.velocity.y) <= jumpHangVelRange && jumpHang) rb.gravityScale = gravityScaleDefault * jumpHangGravScale; //checks the conditions to see if the player is close to the
                                                                                                                                        //precipice of their jump via the player's velocity and the
                                                                                                                                        //velocity range inputted
            else if (rb.velocity.y < 0 && modifyFallGravity) rb.gravityScale = gravityScaleDefault * fallGravityMultiplier; //checks to see if player is falling and if modifying fall gravity
                                                                                                                            //is enabled to then modify the player's gravity scale
            else rb.gravityScale = gravityScaleDefault;                                                                 //if the velocity isn't negative, the gravity scale of the player
                                                                                                                        //is just set to its default.

            if (rb.velocity.y <= -fallSpeedClamp) rb.AddForce(Vector2.up * -(Physics2D.gravity.y + rb.velocity.y +fallSpeedClamp), ForceMode2D.Force);  //checks to see if the player is free-falling
                                                                                                                                                        //and applies a force to keep them from going
                                                                                                                                                        //beyond the clamped speed.

            if(coyoteTimer > 0) coyoteTimer -= Time.fixedDeltaTime; //tick down coyote timer if the player is not grounded
        }
        #endregion

        isGrounded = groundCheck.TestForGrounded();                         //Test to see if the player is grounded to prime them for jumping/crouching next tick
        
        if (isGrounded) isJumping = false;
        if (jumpBufferTimer > 0) jumpBufferTimer -= Time.fixedDeltaTime;    //Tick down jumpBufferTimer
    }

    //Set the direction for horizontal movement based on an axis
    private void OnHorizontalMovement(InputValue movementValue)
    {
        dirX = movementValue.Get<float>();

        if (!limitAerialTurning && Time.timeScale > 0) Turn(); //change the scale based on what direction the player has been facing, swapping around the sprite
                                         //only occurs if the limitAerialTurning is turned off if so, player will always "turn around" to the motion, even in the air
    }

    //Set the direction for vertical movement based on an axis
    private void OnVerticalMovement(InputValue movementValue)
    {
        dirY = movementValue.Get<float>();
        if (dirY < 0 && isGrounded && !isJumping)
        {
            isCrouching = true;
            Debug.Log("You crouch");
        }
        else if (isCrouching)
        {
            isCrouching = false;
            Debug.Log("You are no longer crouching");
        }
    }

    //All jumping (and crouching) button interactions happen here. it is measured on an axis.
    private void OnJump(InputValue jumpValue)
    {
        float jumpAxis = jumpValue.Get<float>();
        
        if (jumpAxis <= 0 && isJumping) //not jumping nor crouching
        {
            isJumping = false;
        }
        else if (jumpAxis > 0 && !isJumping) //trigger to begin a jump
        {
            isJumping = true;
            jumpBufferTimer = jumpBuffer;
        }        
    }
    
    //method for grabbing and dropping blocks
    private void OnGrabDrop(InputValue grabValue)
    {
        grabDrop();
    }

    private void grabDrop()
    {
        if (holding != null) //triggers if the player is currently carrying an object/block
        {
            bool canDrop;
            holding.TryGetComponent(out IGrab grabbable); //test to see if the held object implements IGrab interface (it should as it is needed to be picked up)
            RaycastHit2D dropBoxCast = Physics2D.BoxCast(coll.bounds.center, new Vector2(.95f, .95f), 0.0f, facingRight ? Vector2.right : Vector2.left, 1.05f); //boxcast to determine if the block is
                                                                                                                                                                //partially in a wall
            if (dropBoxCast == true && grabbable != null)
            {
                if (!(Physics2D.BoxCast(coll.bounds.center, coll.bounds.size, 0.0f, facingRight ? Vector2.left : Vector2.right, (1.05f - dropBoxCast.distance)))) //another boxcast behind the player
                                                                                                                                                                  //to determine if they have enough
                                                                                                                                                                  //space to transform away from the
                                                                                                                                                                  //wall and drop the Block
                {
                    //below transforms the player far enough away from the wall/obstruction so that the block can be dropped
                    transform.position = new Vector3(facingRight ? transform.position.x - (1.05f - dropBoxCast.distance) : transform.position.x + (1.05f - dropBoxCast.distance), transform.position.y, transform.position.z);
                    canDrop = true; //dropping is now possible
                }
                else canDrop = false; //since there's not enough space, dropping isn't possible
            }
            else canDrop = true; //since there's nothing in the way of the block, dropping is possible
            if (canDrop)
            {
                grabbable.LetGo();  //informs the Block to run their version of the method, LetGo(), implemented by IGrab
                holding = null;     //set it so the player is holding nothing
                holdColliderObj.SetActive(false);  //collider is no longer necessary
            }

        }
        else //happens if the player isn't holding anything
        {
            RaycastHit2D testBoxCast = Physics2D.BoxCast(coll.bounds.center, coll.bounds.size, 0.0f, facingRight ? Vector2.right : Vector2.left, 0.25f, 512); //boxcast in front of the player for
                                                                                                                                                              //anything on Layer #9
            if (testBoxCast == true)
            {
                GameObject grabQueue = testBoxCast.collider.gameObject; //get a reference to the boxcasted object
                grabQueue.TryGetComponent(out IGrab grabbable);         //test to see if the object implements IGrab
                if (grabbable != null)                                  //IGrab is needed to be grabbed
                {
                    grabQueue.transform.position = new Vector3(facingRight ? coll.bounds.center.x + 1.05f : coll.bounds.center.x - 1.05f, coll.bounds.center.y, 0); //sets the desired position for
                                                                                                                                                                    //the object while being held
                    grabQueue.transform.rotation = Quaternion.identity; //reset the rotation of the object
                    grabbable.Grabbed(gameObject);                      //run the Grabbed() method on the object
                    holding = grabQueue;                                //since the player now holds an object, have holding be a reference to it
                    holdColliderObj.SetActive(true);                       //set the collider to be active now that the player is holding the object
                }
            }
        }
    }

    //method for using animus crystals based on what the player has slotted
    private void OnUseAnimus(InputValue useAnimusValue)
    {
        switch (slottedAnimusCrystal)
        {
            case "neutral":
                Debug.Log("Your crystal does not react");
                break;
            case "ice":
                Debug.Log("You create an ice block");
                break;
            case "fire":
                Debug.Log("You dash forward with flames trailing behind you");
                break;
            default:
                Debug.Log("Nothing happens");
                break;
        }
    }

    //method to ditch the slotted animus crystal
    private void OnDitchAnimus(InputValue ditchAnimusValue)
    {
        slottedAnimusCrystal = "none";
        Debug.Log("You no longer have an Animus Crystal slotted");

        if (!isGrounded)
        {
            Debug.Log("You get an extra jump for ditching your Animus Crystal");
        }
    }

    private void OnPause()
    {
        GameManager.Instance.TogglePauseMenu();
    }

    //method that triggers on the event for landing on a ground. It gets the friction value from the ground that gets artificially applied to the player
    private void GetGroundFriction(Collider2D collision)
    {
        collision.TryGetComponent(out Rigidbody2D rbcol);
        if (rbcol != null) frictionAmount = rbcol.sharedMaterial.friction;                         //use the rigidbody to get the physics material friction
    }

    //turns the player based on player horizontal movement input
    private void Turn()
    {
        if (dirX > 0 && facingRight == false)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            facingRight = true;
        }
        else if (dirX < 0 && facingRight == true)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            facingRight = false;
        }
    }

    //the jump method that propels the player
    private void Jump()
    {
        if (rb.velocity.y < 0) rb.AddForce((-1 * rb.velocity), ForceMode2D.Impulse); //negates the negative velocity if needed
        rb.AddForce(initialJumpVelocity, ForceMode2D.Impulse); //the jump itself
        if (willJumpBoost) rb.AddForce(Vector2.right * targetVelocity * (isGrounded || isOnMovingPlatform ? jumpBoostMultiplier : jumpBoostCoyoteMultiplier), ForceMode2D.Impulse); //horizontal jump boost if enabled
        coyoteTimer = 0;        //coyote no longer possible/just executed
        jumpBufferTimer = 0;    //jumpBufferTimer turn off at start of jump as jump has just been executed
    }
    private void StopCrouching()
    {
        isCrouching = false;
        Debug.Log("You are no longer crouching");
    }

    
}
