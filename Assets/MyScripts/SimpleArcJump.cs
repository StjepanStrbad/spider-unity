using UnityEngine;

public class SimpleArcJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [Range(1f, 10f)]
    public float jumpHeight = 4f;
    [Range(0.5f, 5f)]
    public float jumpSpeed = 3f;
    public KeyCode jumpKey = KeyCode.Space;

    [Header("Arc Physics")]
    public LayerMask surfaceMask = -1;
    [Range(0.1f, 1f)]
    public float spiderRadius = 0.3f;

    [Header("Surface Safety")]
    [Range(0f, 180f)]
    public float maxSurfaceAngle = 90f; // Maximum surface angle to jump from
    [Range(0.1f, 2f)]
    public float surfaceOffset = 0.2f; // How far to offset from surface when jumping

    [Header("Jump Animation")]
    [Range(0f, 1f)]
    public float windupAmount = 0.3f; // How much to pull back before jumping
    [Range(0.1f, 1f)]
    public float windupDuration = 0.2f; // How long the windup lasts
    [Range(0f, 2f)]
    public float legTuckHeight = 0.5f; // How much legs tuck up during flight
    [Range(1f, 10f)]
    public float legAnimationSpeed = 4f; // Speed of leg animations

    [Header("Air Correction")]
    [Range(0f, 20f)]
    public float airRotationSpeed = 8f; // How fast spider corrects rotation in air

    [Header("Debug")]
    public bool showDebugPath = true;

    // References
    private GroundStick groundStick;
    private Transform bodyTransform;

    // Jump state
    private bool isJumping = false;
    private bool isWindingUp = false;
    private Vector3 startPos;
    private Vector3 jumpDirection;
    private Vector3 surfaceNormal;
    private float arcTime = 0f;
    private float windupTime = 0f;
    private Quaternion initialRotation;
    private Vector3 originalBodyPosition;
    private Vector3 windupOffset;

    void Start()
    {
        groundStick = GetComponent<GroundStick>();
        bodyTransform = GameObject.Find("myOwn")?.transform ?? transform;
    }

    void Update()
    {
        if (Input.GetKeyDown(jumpKey) && !isJumping && !isWindingUp)
        {
            if (CanPerformJump())
            {
                StartWindup();
            }
            else
            {
                Debug.Log("Cannot jump from this surface angle or no surface detected");
            }
        }

        if (isWindingUp)
        {
            ExecuteWindup();
        }
        else if (isJumping)
        {
            ExecuteJump();
        }
    }

    private bool CanPerformJump()
    {
        // Check if we have a valid surface to jump from
        if (groundStick == null || !groundStick.IsOnSurface)
        {
            return false;
        }

        // Get current surface normal
        Vector3 currentSurfaceNormal = groundStick.CurrentSurfaceNormal;

        // Check surface angle (angle between surface normal and world up)
        float surfaceAngle = Vector3.Angle(currentSurfaceNormal, Vector3.up);

        if (surfaceAngle > maxSurfaceAngle)
        {
            Debug.Log($"Surface too steep to jump from. Angle: {surfaceAngle:F1}�, Max allowed: {maxSurfaceAngle:F1}�");
            return false;
        }

        return true;
    }

    private void StartWindup()
    {
        // Get jump direction for windup
        jumpDirection = GetJumpDirection();
        surfaceNormal = groundStick.CurrentSurfaceNormal;

        // Store original body position
        originalBodyPosition = bodyTransform.position;

        // Calculate windup offset (pull back and down slightly)
        Vector3 backwardOffset = -jumpDirection * windupAmount * 0.5f;
        Vector3 downwardOffset = -surfaceNormal * windupAmount * 0.3f;
        windupOffset = backwardOffset + downwardOffset;

        // Start windup
        isWindingUp = true;
        windupTime = 0f;

        Debug.Log("Starting jump windup...");
    }

    private void ExecuteWindup()
    {
        windupTime += Time.deltaTime;

        // Animate body pulling back
        float windupProgress = Mathf.Clamp01(windupTime / windupDuration);
        float windupCurve = Mathf.Sin(windupProgress * Mathf.PI * 0.5f); // Smooth curve

        Vector3 currentWindupPos = Vector3.Lerp(originalBodyPosition, originalBodyPosition + windupOffset, windupCurve);
        bodyTransform.position = currentWindupPos;

        // Animate legs tucking slightly during windup
        AnimateLegsForWindup(windupProgress);

        // End windup and start jump
        if (windupTime >= windupDuration)
        {
            StartJump();
        }
    }

    private void StartJump()
    {
        // End windup state
        isWindingUp = false;

        // Reset body to original position before jump
        bodyTransform.position = originalBodyPosition;

        // Start position offset from surface to prevent clipping
        startPos = bodyTransform.position + surfaceNormal * surfaceOffset;

        // Store initial rotation for air correction
        initialRotation = bodyTransform.rotation;

        // Start jump
        isJumping = true;
        arcTime = 0f;

        // Disable ground stick
        if (groundStick != null)
            groundStick.enabled = false;

        Debug.Log($"Starting arc jump toward {jumpDirection} from surface angle: {Vector3.Angle(surfaceNormal, Vector3.up):F1}�");
    }

    private Vector3 GetJumpDirection()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // No input - use forward
        if (Mathf.Abs(horizontal) < 0.1f && Mathf.Abs(vertical) < 0.1f)
        {
            return bodyTransform.forward;
        }

        // Use camera-relative direction
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            Vector3 right = cam.transform.right;

            // Project to horizontal plane
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 inputDirection = (forward * vertical + right * horizontal).normalized;

            // Ensure jump direction doesn't go into the surface we're standing on
            if (groundStick != null && groundStick.IsOnSurface)
            {
                Vector3 currentSurfaceNormal = groundStick.CurrentSurfaceNormal;
                // If jumping direction would go into surface, project it onto surface plane
                if (Vector3.Dot(inputDirection, currentSurfaceNormal) < -0.1f)
                {
                    inputDirection = Vector3.ProjectOnPlane(inputDirection, currentSurfaceNormal).normalized;
                }
            }

            return inputDirection;
        }

        return bodyTransform.forward;
    }

    private void ExecuteJump()
    {
        // Get current position on arc
        Vector3 currentPos = bodyTransform.position;

        // Calculate next position - made jump half as slow
        arcTime += Time.deltaTime * jumpSpeed * 0.5f;
        Vector3 nextPos = CalculateArcPosition(arcTime);

        // Handle air rotation correction
        HandleAirRotation();

        // Handle leg animations during flight
        HandleLegAnimations();

        // Check if the movement from current to next position hits anything
        Vector3 movement = nextPos - currentPos;
        float movementDistance = movement.magnitude;

        if (movementDistance > 0.001f)
        {
            Vector3 movementDir = movement.normalized;

            // Use SphereCast to check if the spider (as a sphere) would hit something
            if (Physics.SphereCast(currentPos, spiderRadius, movementDir, out RaycastHit hit, movementDistance, surfaceMask))
            {
                // Hit something! Land at the hit point
                Vector3 landingPos = hit.point + hit.normal * spiderRadius;
                bodyTransform.position = landingPos;
                EndJump();
                return;
            }
        }

        // No collision, continue arc
        bodyTransform.position = nextPos;

        // Safety check - if we've gone way too far, end the jump
        if (arcTime > 10f) // Simple time-based safety check instead of distance
        {
            EndJump();
        }
    }

    private Vector3 CalculateArcPosition(float time)
    {
        // Horizontal movement (constant velocity)
        Vector3 horizontalMovement = jumpDirection * jumpSpeed * time;

        // Vertical movement (ballistic - up then down due to gravity)
        float initialVerticalVelocity = jumpHeight * 2f; // Adjust this for desired arc height
        float gravity = 9.81f;
        float verticalMovement = (initialVerticalVelocity * time) - (0.5f * gravity * time * time);

        return startPos + horizontalMovement + Vector3.up * verticalMovement;
    }

    private void HandleAirRotation()
    {
        if (airRotationSpeed <= 0f) return;

        float jumpProgress = Mathf.Clamp01(arcTime / 3f);

        // Target rotation: face the jump direction with world-up.
        // Early in the jump we blend in from the launch rotation so it doesn't snap.
        Quaternion lookRot = Quaternion.LookRotation(jumpDirection, Vector3.up);
        Quaternion targetRotation = jumpProgress < 0.2f
            ? Quaternion.Slerp(initialRotation, lookRot, jumpProgress * 5f)
            : lookRot;

        // Ease off the correction near landing so the touchdown doesn't get jerked.
        float rotationSpeedMultiplier = jumpProgress > 0.7f ? 1.5f : 2f;

        bodyTransform.rotation = Quaternion.Slerp(
            bodyTransform.rotation,
            targetRotation,
            Time.deltaTime * airRotationSpeed * rotationSpeedMultiplier
        );
    }

    private void AnimateLegsForWindup(float windupProgress)
    {
        if (groundStick == null) return;

        // Get legs from GroundStick
        var legs = new[] { groundStick.frontLeft, groundStick.frontRight, groundStick.backLeft, groundStick.backRight };

        // Slight leg tuck during windup (much smaller than flight tuck)
        float windupTuckAmount = windupProgress * 0.2f;

        foreach (var leg in legs)
        {
            if (leg?.target != null)
            {
                Vector3 originalPos = leg.lastPlantedPosition;
                Vector3 tuckOffset = surfaceNormal * windupTuckAmount;
                leg.target.transform.position = Vector3.Lerp(originalPos, originalPos + tuckOffset, windupProgress);

                if (leg.foot != null)
                {
                    leg.foot.transform.position = leg.target.transform.position;
                }
            }
        }
    }

    private void HandleLegAnimations()
    {
        if (groundStick == null) return;

        // Calculate jump progress (0 to 1)
        float jumpProgress = Mathf.Clamp01(arcTime / 3f);

        // Get legs from GroundStick
        var legs = new[] { groundStick.frontLeft, groundStick.frontRight, groundStick.backLeft, groundStick.backRight };

        foreach (var leg in legs)
        {
            if (leg?.target != null)
            {
                Vector3 originalPos = leg.lastPlantedPosition;
                Vector3 targetLegPos = originalPos;

                if (jumpProgress < 0.3f)
                {
                    // Early flight - legs tuck up quickly
                    float tuckProgress = jumpProgress / 0.3f;
                    float tuckCurve = Mathf.Sin(tuckProgress * Mathf.PI * 0.5f);
                    Vector3 tuckOffset = bodyTransform.up * legTuckHeight * tuckCurve;
                    targetLegPos = originalPos + tuckOffset;
                }
                else if (jumpProgress < 0.7f)
                {
                    // Mid-flight - legs stay tucked
                    Vector3 tuckOffset = bodyTransform.up * legTuckHeight;
                    targetLegPos = originalPos + tuckOffset;
                }
                else
                {
                    // Landing preparation - legs extend back down
                    float extendProgress = (jumpProgress - 0.7f) / 0.3f;
                    float extendCurve = Mathf.Sin(extendProgress * Mathf.PI * 0.5f);
                    Vector3 tuckOffset = bodyTransform.up * legTuckHeight * (1f - extendCurve);
                    targetLegPos = originalPos + tuckOffset;
                }

                // Smoothly animate to target position
                leg.target.transform.position = Vector3.Lerp(
                    leg.target.transform.position,
                    targetLegPos,
                    Time.deltaTime * legAnimationSpeed
                );

                if (leg.foot != null)
                {
                    leg.foot.transform.position = leg.target.transform.position;
                }
            }
        }
    }

    private void EndJump()
    {
        isJumping = false;

        // Re-enable ground stick
        if (groundStick != null)
            groundStick.enabled = true;

        Debug.Log("Arc jump completed - surface hit!");
    }

    // Public properties
    public bool IsJumping => isJumping;
    public bool IsWindingUp => isWindingUp;

    void OnDrawGizmosSelected()
    {
        if (!showDebugPath) return;

        // Null checks to prevent errors when game stops
        if (bodyTransform == null || groundStick == null) return;

        Vector3 start = isJumping ? startPos : (bodyTransform.position + (groundStick?.CurrentSurfaceNormal ?? Vector3.up) * surfaceOffset);
        Vector3 direction = isJumping ? jumpDirection : GetJumpDirection();

        // Draw the arc path
        Vector3 lastPoint = start;
        float timeStep = 0.1f;

        Gizmos.color = isJumping ? Color.cyan : Color.green;

        // Draw arc for several seconds to show the full trajectory
        for (float t = timeStep; t <= 10f; t += timeStep)
        {
            Vector3 horizontalMovement = direction * jumpSpeed * t;
            float initialVerticalVelocity = jumpHeight * 2f;
            float gravity = 9.81f;
            float verticalMovement = (initialVerticalVelocity * t) - (0.5f * gravity * t * t);

            Vector3 arcPoint = start + horizontalMovement + Vector3.up * verticalMovement;

            // Stop drawing if we've gone below reasonable height
            if (arcPoint.y < start.y - 50f) break;

            Gizmos.DrawLine(lastPoint, arcPoint);
            lastPoint = arcPoint;
        }

        // Draw spider as sphere
        if (isJumping && bodyTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bodyTransform.position, spiderRadius);
        }

        // Draw surface normal and jump start position
        if (groundStick != null && groundStick.IsOnSurface && bodyTransform != null)
        {
            Vector3 surfaceNorm = groundStick.CurrentSurfaceNormal;
            Vector3 bodyPos = bodyTransform.position;

            // Draw surface normal
            Gizmos.color = Color.red;
            Gizmos.DrawRay(bodyPos, surfaceNorm * 1f);

            // Draw jump start position
            Gizmos.color = Color.blue;
            Vector3 jumpStartPos = bodyPos + surfaceNorm * surfaceOffset;
            Gizmos.DrawWireSphere(jumpStartPos, 0.1f);

            // Show surface angle warning
            float angle = Vector3.Angle(surfaceNorm, Vector3.up);
            if (angle > maxSurfaceAngle)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bodyPos + Vector3.up * 0.5f, Vector3.one * 0.1f);
            }
        }
    }
    public void ResetJump()
    {
        isJumping = false;
        isWindingUp = false;
        arcTime = 0f;
        windupTime = 0f;

        if (groundStick != null)
            groundStick.enabled = true;
    }
}