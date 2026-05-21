using UnityEngine;

public class SpiderCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public GroundStick spiderController;
    public SimpleArcJump arcJump;

    [Header("Camera Settings")]
    public float mouseSensitivity = 4f;
    public float distanceMin = 1.5f;
    public float distanceMax = 6f;
    public float initialDistance = 4f;
    public float scrollSensitivity = 2f;
    public float smoothSpeed = 10f;

    [Header("Free Cam Settings")]
    public float freeCamSensitivity = 4f;
    public float freeCamReturnSpeed = 5f;

    [Header("Surface Following")]
    public float surfaceAlignSpeed = 3f;
    public bool followSpiderOrientation = true;

    [Header("Jump Following")]
    public float jumpAlignSpeed = 8f;
    public bool followDuringJumps = true;

    [Header("Orbit Constraints")]
    [Range(5f, 89f)]   public float minPitchAngle = 10f;   // how close camera can get to directly above spider
    [Range(91f, 175f)] public float maxPitchAngle = 160f;  // how far below the spider the camera can go

    private float distance;
    private Vector3 currentUp = Vector3.up;

    // World-space direction from spider to camera.
    // Rotated by the surface-normal delta each frame so the camera tracks
    // smoothly through surface transitions without snapping.
    private Vector3 camDir;
    private Vector3 freeCamDir;
    private bool isFreeCamActive = false;

    // Velocity state for critically-damped smoothing of currentUp.
    // SmoothDamp filters jitter (from GroundStick leg-averaging during corner traversal)
    // much better than Slerp, while still reaching the target without overshoot.
    private Vector3 upVelocity = Vector3.zero;

    public bool IsFreeCamActive => isFreeCamActive;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("SpiderCamera: Target not assigned.");
            enabled = false;
            return;
        }

        if (spiderController == null) spiderController = FindFirstObjectByType<GroundStick>();
        if (arcJump == null)          arcJump          = FindFirstObjectByType<SimpleArcJump>();

        distance = initialDistance;

        Vector3 toCamera = transform.position - target.position;
        camDir     = toCamera.sqrMagnitude > 0.001f ? toCamera.normalized : new Vector3(0f, 0.5f, -1f).normalized;
        freeCamDir = camDir;
        currentUp  = Vector3.up;
    }

    void LateUpdate()
    {
        if (target == null) return;

        UpdateSurfaceAlignment();
        HandleInput();
        UpdateCameraPosition();
    }

    private void UpdateSurfaceAlignment()
    {
        Vector3 targetUp = GetTargetUp();
        Vector3 oldUp    = currentUp;

        // Critically-damped smoothing filters jitter from GroundStick's leg-averaging.
        float smoothTime = 1f / Mathf.Max(GetAlignSpeed(), 0.01f);
        currentUp = Vector3.SmoothDamp(currentUp, targetUp, ref upVelocity, smoothTime).normalized;

        if (Vector3.Angle(oldUp, currentUp) > 0.02f)
        {
            ApplyUpDelta(ref camDir, oldUp);
            ApplyUpDelta(ref freeCamDir, oldUp);
        }
    }

    // Rotates a camera orbit vector to follow a change in currentUp.
    // The pitch axis is derived from the *camera's own surface position* (Cross of
    // currentUp and the orbit vector's surface projection) — NOT from the spider's
    // body right. target.right has discontinuities during over-the-edge transitions
    // (it passes through zero and flips sign when spider's forward direction flips),
    // which caused the camera to get stuck looking top-down. The camera-derived right
    // axis is stable through any spider rotation, including 180° flips.
    private void ApplyUpDelta(ref Vector3 dir, Vector3 oldUp)
    {
        Vector3 surfaceDir = Vector3.ProjectOnPlane(dir, currentUp);
        if (surfaceDir.sqrMagnitude < 0.0001f)
        {
            // Camera directly above/below spider — degenerate. Fall back to spider's forward.
            surfaceDir = Vector3.ProjectOnPlane(target.forward, currentUp);
            if (surfaceDir.sqrMagnitude < 0.0001f) return;
        }
        surfaceDir = surfaceDir.normalized;

        Vector3 rightAxis = Vector3.Cross(currentUp, surfaceDir);
        if (rightAxis.sqrMagnitude < 0.0001f) return;
        rightAxis = rightAxis.normalized;

        float pitchAngle = Vector3.SignedAngle(oldUp, currentUp, rightAxis);
        if (Mathf.Abs(pitchAngle) > 0.02f)
        {
            Quaternion upDelta = Quaternion.AngleAxis(pitchAngle, rightAxis);
            dir = (upDelta * dir).normalized;
        }
    }

    private Vector3 GetTargetUp()
    {
        bool isJumping = arcJump != null && arcJump.IsJumping;
        if (isJumping && followDuringJumps) return target.up;
        if (!followSpiderOrientation || spiderController == null) return Vector3.up;
        // Use the spider body's actual up directly — GroundStick already smooths it via
        // upAlignSpeed, so adding a second camera Slerp just creates unwanted lag on transitions.
        return target.up;
    }

    private float GetAlignSpeed()
    {
        bool isJumping = arcJump != null && arcJump.IsJumping;
        return (isJumping && followDuringJumps) ? jumpAlignSpeed : surfaceAlignSpeed;
    }

    private void HandleInput()
    {
        bool rightMouseHeld = Input.GetMouseButton(1);

        if (rightMouseHeld && !isFreeCamActive)
        {
            isFreeCamActive = true;
            freeCamDir = camDir;
        }
        else if (!rightMouseHeld && isFreeCamActive)
        {
            isFreeCamActive = false;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (isFreeCamActive)
        {
            RotateOrbitDir(ref freeCamDir, mouseX * freeCamSensitivity, mouseY * freeCamSensitivity);
        }
        else
        {
            RotateOrbitDir(ref camDir, mouseX * mouseSensitivity, mouseY * mouseSensitivity);
            // Blend freeCamDir back so releasing RMB doesn't snap
            freeCamDir = Vector3.Slerp(freeCamDir, camDir, Time.deltaTime * freeCamReturnSpeed).normalized;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance = Mathf.Clamp(distance - scroll * scrollSensitivity, distanceMin, distanceMax);

        if (Input.GetKeyDown(KeyCode.C)) followSpiderOrientation = !followSpiderOrientation;
        if (Input.GetKeyDown(KeyCode.V)) followDuringJumps = !followDuringJumps;
    }

    // Rotates the orbit direction in-place.
    // Yaw  (mouseX) → around currentUp, always consistent regardless of surface angle.
    // Pitch (mouseY) → around the orbit's right axis, clamped to avoid poles.
    // If dir is degenerately parallel to currentUp (would normally lock the camera),
    // we use a fallback right axis so the user can always rotate back to a valid orbit.
    private void RotateOrbitDir(ref Vector3 dir, float yawDelta, float pitchDelta)
    {
        if (Mathf.Abs(yawDelta) > 0.001f)
            dir = Quaternion.AngleAxis(yawDelta, currentUp) * dir;

        if (Mathf.Abs(pitchDelta) > 0.001f)
        {
            Vector3 right = Vector3.Cross(currentUp, dir);
            if (right.sqrMagnitude < 0.0001f)
            {
                // dir is parallel to currentUp — pick any axis perpendicular to currentUp
                // so the user can pitch out of the degenerate state.
                Vector3 reference = Mathf.Abs(Vector3.Dot(currentUp, Vector3.forward)) < 0.95f
                    ? Vector3.forward : Vector3.right;
                right = Vector3.Cross(currentUp, reference);
            }
            right = right.normalized;

            Vector3 candidate     = Quaternion.AngleAxis(pitchDelta, right) * dir;
            float   candidateAngle = Vector3.Angle(candidate, currentUp);
            float   currentAngle   = Vector3.Angle(dir, currentUp);

            bool inRange = candidateAngle > minPitchAngle && candidateAngle < maxPitchAngle;
            // Allow rotation that moves *toward* the valid range, even if the result is still outside.
            // This is what lets the user escape a stuck top-down or bottom-up state.
            bool recovering = (currentAngle <= minPitchAngle && candidateAngle > currentAngle) ||
                              (currentAngle >= maxPitchAngle && candidateAngle < currentAngle);

            if (inRange || recovering)
                dir = candidate;
        }

        dir = dir.normalized;
    }

    private void UpdateCameraPosition()
    {
        Vector3 activeDir = isFreeCamActive ? freeCamDir : camDir;

        Vector3 desiredPosition = target.position + activeDir * distance;

        bool  isJumping          = arcJump != null && arcJump.IsJumping;
        float currentSmoothSpeed = isJumping && followDuringJumps ? smoothSpeed * 1.5f : smoothSpeed;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * currentSmoothSpeed);

        Vector3 lookTarget = target.position + currentUp * 0.5f;
        Vector3 lookDir    = (lookTarget - transform.position).normalized;

        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion lookRot = Quaternion.LookRotation(lookDir, currentUp);
            float rotSpeed     = isJumping && followDuringJumps ? smoothSpeed * 2f : smoothSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotSpeed);
        }

        Debug.DrawRay(target.position, currentUp * 2f, Color.yellow);
        Debug.DrawRay(transform.position, transform.forward * 3f, isFreeCamActive ? Color.cyan : Color.red);
        if (isJumping) Debug.DrawRay(target.position + Vector3.right * 0.5f, Vector3.up * 2f, Color.cyan);
    }
}
