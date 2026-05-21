using UnityEngine;
using System.Collections.Generic;

public class GroundStick : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        public GameObject helper;
        public GameObject target;
        public GameObject foot;

        public Vector3 lastPlantedPosition;
        public float stepThreshold = 0.5f;
        public float maxStepDistance = 1.2f;
        public float stepSpeed = 5f;
        public float stepHeight = 0.3f;
        public Color debugColor;
        // Enhanced surface detection properties
        public bool isOnValidSurface = false;
        public Vector3 surfaceNormal = Vector3.up;
        public float surfaceDistance = 0f;
        public Vector3 surfacePoint = Vector3.zero;
        // Enhanced lateral and multi-directional surface detection
        public bool hasLateralSurface = false;
        public Vector3 lateralSurfaceNormal = Vector3.up;
        public float lateralSurfaceDistance = float.MaxValue;
        public Vector3 lateralSurfacePoint = Vector3.zero;
        // Downward surface detection
        public bool hasGroundBelow = false;
        public Vector3 downwardSurfaceNormal = Vector3.up;
        public float downwardSurfaceDistance = float.MaxValue;
        public Vector3 downwardSurfacePoint = Vector3.zero;
        // Multi-directional surface detection for corners
        public SurfaceInfo[] multiDirectionalSurfaces = new SurfaceInfo[8];
        public int validSurfaceCount = 0;
        // Stepping state
        private Vector3 stepStart;
        private Vector3 stepEnd;
        private float stepProgress = 1f;
        private bool wantsToStep = false;
        private bool isStepCompleted = true;
        // Stability tracking
        private float timeSinceLastStep = 0f;
        private float minStepInterval = 0.2f;
        private Vector3 lastValidStepTarget;
        private bool hasValidStepTarget = false;

        public bool IsStepping => stepProgress < 1f;
        public bool WantsToStep => wantsToStep && timeSinceLastStep > minStepInterval;
        public float DistanceFromLastPlant => Vector3.Distance(lastPlantedPosition, target.transform.position);

        [System.Serializable]
        public class SurfaceInfo
        {
            public bool isValid = false;
            public Vector3 normal = Vector3.up;
            public Vector3 point = Vector3.zero;
            public float distance = float.MaxValue;
            public Vector3 direction = Vector3.zero;
            public float confidence = 0f;

            public void Set(Vector3 norm, Vector3 pt, float dist, Vector3 dir, float conf)
            {
                isValid = true;
                normal = norm;
                point = pt;
                distance = dist;
                direction = dir;
                confidence = conf;
            }

            public void Clear()
            {
                isValid = false;
                distance = float.MaxValue;
                confidence = 0f;
            }
        }

        public void UpdateLeg(LayerMask groundMask, float raycastDistance, Vector3 moveDirection, Vector3 currentBodyUp, bool isMoving, Transform bodyTransform, bool isSprinting = false)
        {
            timeSinceLastStep += Time.deltaTime;

            // Enhanced multi-directional surface detection
            DetectSurfacesInAllDirections(groundMask, raycastDistance, bodyTransform);

            // Calculate best surface for stepping
            Vector3 bestStepTarget = Vector3.zero;
            Vector3 bestSurfaceNormal = Vector3.up;
            bool foundValidStepTarget = false;

            // Primary surface detection (existing logic enhanced)
            Vector3 origin = helper.transform.position + currentBodyUp * 0.5f;

            // Enhanced primary surface casting
            Vector3[] primaryDirections = {
                -currentBodyUp,
                -currentBodyUp + bodyTransform.forward * 0.3f,
                -currentBodyUp - bodyTransform.forward * 0.3f,
                -currentBodyUp + bodyTransform.right * 0.3f,
                -currentBodyUp - bodyTransform.right * 0.3f,
                // Enhanced corner detection
                -currentBodyUp + (bodyTransform.forward + bodyTransform.right).normalized * 0.4f,
                -currentBodyUp + (bodyTransform.forward - bodyTransform.right).normalized * 0.4f,
                -currentBodyUp + (-bodyTransform.forward + bodyTransform.right).normalized * 0.4f,
                -currentBodyUp + (-bodyTransform.forward - bodyTransform.right).normalized * 0.4f
            };

            RaycastHit bestPrimaryHit = new RaycastHit();
            bool foundPrimarySurface = false;
            float closestPrimaryDistance = float.MaxValue;

            foreach (Vector3 direction in primaryDirections)
            {
                if (Physics.SphereCast(origin, 0.1f, direction.normalized, out RaycastHit hit, raycastDistance, groundMask))
                {
                    if (hit.distance < closestPrimaryDistance)
                    {
                        closestPrimaryDistance = hit.distance;
                        bestPrimaryHit = hit;
                        foundPrimarySurface = true;
                    }
                }
            }

            // Update primary surface info
            if (foundPrimarySurface)
            {
                isOnValidSurface = true;
                surfaceNormal = bestPrimaryHit.normal;
                surfaceDistance = bestPrimaryHit.distance;
                surfacePoint = bestPrimaryHit.point;
                bestStepTarget = bestPrimaryHit.point;
                bestSurfaceNormal = bestPrimaryHit.normal;
                foundValidStepTarget = true;

                Debug.DrawRay(origin, -currentBodyUp * bestPrimaryHit.distance, debugColor);
            }
            else
            {
                isOnValidSurface = false;
                surfaceDistance = float.MaxValue;
            }

            // Enhanced lateral surface detection for side transitions
            DetectLateralSurfaces(groundMask, raycastDistance, bodyTransform, currentBodyUp);

            // If no primary surface but we have lateral surfaces, use the best lateral surface
            if (!foundValidStepTarget && hasLateralSurface)
            {
                bestStepTarget = lateralSurfacePoint;
                bestSurfaceNormal = lateralSurfaceNormal;
                foundValidStepTarget = true;
                isOnValidSurface = true; // Consider lateral surface as valid
            }

            // Enhanced multi-directional surface analysis for corner transitions
            if (!foundValidStepTarget)
            {
                SurfaceInfo bestCornerSurface = GetBestCornerSurface(moveDirection, bodyTransform);
                if (bestCornerSurface.isValid && bestCornerSurface.confidence > 0.3f)
                {
                    bestStepTarget = bestCornerSurface.point;
                    bestSurfaceNormal = bestCornerSurface.normal;
                    foundValidStepTarget = true;
                    isOnValidSurface = true;
                }
            }

            // Enhanced downward surface detection
            DetectDownwardSurfaces(groundMask, raycastDistance, bodyTransform);

            // Use downward surface if no other options
            if (!foundValidStepTarget && hasGroundBelow)
            {
                bestStepTarget = downwardSurfacePoint;
                bestSurfaceNormal = downwardSurfaceNormal;
                foundValidStepTarget = true;
            }

            // Calculate step target with enhanced prediction
            if (foundValidStepTarget)
            {
                Vector3 predictionDir = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : bodyTransform.forward;
                float basePredictionDistance = isMoving ? 0.4f : 0.2f;

                // Increase prediction distance when sprinting for more fluid movement
                if (isSprinting && isMoving)
                {
                    basePredictionDistance *= 1.6f;
                }

                // Enhanced prediction based on surface type and movement
                if (hasLateralSurface && Vector3.Dot(moveDirection, lateralSurfaceNormal) < -0.3f)
                {
                    // Moving toward lateral surface - increase prediction
                    basePredictionDistance *= 1.5f;
                    predictionDir = Vector3.Slerp(predictionDir, -lateralSurfaceNormal, 0.4f);
                }

                Vector3 predictedStepTarget = bestStepTarget + Vector3.ProjectOnPlane(predictionDir, bestSurfaceNormal) * basePredictionDistance;

                // Smooth step target to reduce jitter
                if (hasValidStepTarget)
                {
                    predictedStepTarget = Vector3.Lerp(lastValidStepTarget, predictedStepTarget, 0.7f);
                }
                lastValidStepTarget = predictedStepTarget;
                hasValidStepTarget = true;

                // Enhanced stepping decision logic
                float dist = Vector3.Distance(lastPlantedPosition, predictedStepTarget);
                bool shouldStep = ShouldTakeStep(dist, moveDirection, isMoving, bodyTransform);

                if (shouldStep)
                {
                    wantsToStep = true;
                    stepEnd = predictedStepTarget;
                    isStepCompleted = false;
                }
            }
            else
            {
                isOnValidSurface = false;
                hasValidStepTarget = false;

                // Emergency fallback positioning
                if (!IsStepping && Vector3.Distance(helper.transform.position, lastPlantedPosition) > maxStepDistance * 1.2f)
                {
                    Vector3 fallbackTarget = helper.transform.position - currentBodyUp * 1f;
                    wantsToStep = true;
                    stepEnd = fallbackTarget;
                    isStepCompleted = false;
                }
            }

            // Handle stepping animation
            if (IsStepping)
            {
                stepProgress += Time.deltaTime * stepSpeed;
                stepProgress = Mathf.Clamp01(stepProgress);

                Vector3 flatPos = Vector3.Lerp(stepStart, stepEnd, stepProgress);
                float arc = 4 * stepHeight * stepProgress * (1 - stepProgress);
                flatPos += bestSurfaceNormal * arc;

                target.transform.position = flatPos;

                if (stepProgress >= 1f && !isStepCompleted)
                {
                    lastPlantedPosition = stepEnd;
                    target.transform.position = lastPlantedPosition;
                    wantsToStep = false;
                    isStepCompleted = true;
                    timeSinceLastStep = 0f;

                    if (foot != null)
                    {
                        foot.transform.position = lastPlantedPosition;
                    }
                }
            }
            else
            {
                target.transform.position = lastPlantedPosition;
                if (foot != null)
                {
                    foot.transform.position = lastPlantedPosition;
                }
            }
        }

        private void DetectSurfacesInAllDirections(LayerMask groundMask, float raycastDistance, Transform bodyTransform)
        {
            // Clear previous detections
            for (int i = 0; i < multiDirectionalSurfaces.Length; i++)
            {
                multiDirectionalSurfaces[i].Clear();
            }
            validSurfaceCount = 0;

            Vector3 origin = helper.transform.position;

            // 8-directional surface detection for comprehensive corner handling
            Vector3[] searchDirections = {
                bodyTransform.forward,           // 0: Forward
                -bodyTransform.forward,          // 1: Backward
                bodyTransform.right,             // 2: Right
                -bodyTransform.right,            // 3: Left
                bodyTransform.up,                // 4: Up
                -bodyTransform.up,               // 5: Down
                (bodyTransform.forward + bodyTransform.right).normalized,   // 6: Forward-Right
                (bodyTransform.forward - bodyTransform.right).normalized    // 7: Forward-Left
            };

            for (int i = 0; i < searchDirections.Length; i++)
            {
                Vector3 searchDir = searchDirections[i];

                if (Physics.Raycast(origin, searchDir, out RaycastHit hit, raycastDistance * 1.5f, groundMask))
                {
                    // Calculate confidence based on distance and angle
                    float distanceConfidence = 1f - (hit.distance / (raycastDistance * 1.5f));
                    float angleConfidence = Mathf.Abs(Vector3.Dot(hit.normal, -searchDir));
                    float totalConfidence = (distanceConfidence + angleConfidence) * 0.5f;

                    if (totalConfidence > 0.2f)
                    {
                        multiDirectionalSurfaces[i].Set(hit.normal, hit.point, hit.distance, searchDir, totalConfidence);
                        validSurfaceCount++;

                        // Debug visualization
                        Debug.DrawRay(origin, searchDir * hit.distance, Color.white * totalConfidence, 0.1f);
                    }
                }
            }
        }

        private void DetectLateralSurfaces(LayerMask groundMask, float raycastDistance, Transform bodyTransform, Vector3 currentBodyUp)
        {
            Vector3 origin = helper.transform.position;

            Vector3[] lateralDirections = {
                bodyTransform.right,
                -bodyTransform.right,
                bodyTransform.forward,
                -bodyTransform.forward,
                (bodyTransform.right + bodyTransform.forward * 0.5f).normalized,
                (-bodyTransform.right + bodyTransform.forward * 0.5f).normalized,
                (bodyTransform.right - bodyTransform.forward * 0.5f).normalized,
                (-bodyTransform.right - bodyTransform.forward * 0.5f).normalized
            };

            RaycastHit bestLateralHit = new RaycastHit();
            bool foundLateralSurface = false;
            float bestLateralScore = 0f;

            foreach (Vector3 lateralDir in lateralDirections)
            {
                if (Physics.Raycast(origin, lateralDir, out RaycastHit lateralHit, raycastDistance * 1.2f, groundMask))
                {
                    // Score based on distance, angle, and suitability for stepping
                    float distanceScore = 1f - (lateralHit.distance / (raycastDistance * 1.2f));
                    float angleScore = Mathf.Abs(Vector3.Dot(lateralHit.normal, -lateralDir));
                    float stepabilityScore = Vector3.Dot(lateralHit.normal, currentBodyUp) > 0.1f ? 1f : 0.5f;

                    float totalScore = distanceScore * angleScore * stepabilityScore;

                    if (totalScore > bestLateralScore && totalScore > 0.3f)
                    {
                        bestLateralScore = totalScore;
                        bestLateralHit = lateralHit;
                        foundLateralSurface = true;
                    }
                }
            }

            if (foundLateralSurface)
            {
                hasLateralSurface = true;
                lateralSurfaceNormal = bestLateralHit.normal;
                lateralSurfaceDistance = bestLateralHit.distance;
                lateralSurfacePoint = bestLateralHit.point;

                Debug.DrawRay(origin, (bestLateralHit.point - origin).normalized * bestLateralHit.distance, Color.green, 0.1f);
            }
            else
            {
                hasLateralSurface = false;
                lateralSurfaceDistance = float.MaxValue;
            }
        }

        private void DetectDownwardSurfaces(LayerMask groundMask, float raycastDistance, Transform bodyTransform)
        {
            Vector3 downwardOrigin = helper.transform.position;
            Vector3[] downwardDirections = {
                Vector3.down,
                Vector3.down + bodyTransform.forward * 0.4f,
                Vector3.down - bodyTransform.forward * 0.4f,
                Vector3.down + bodyTransform.right * 0.4f,
                Vector3.down - bodyTransform.right * 0.4f,
                Vector3.down + bodyTransform.forward * 0.6f,
                Vector3.down - bodyTransform.forward * 0.6f,
                Vector3.down + bodyTransform.right * 0.6f,
                Vector3.down - bodyTransform.right * 0.6f
            };

            RaycastHit bestDownwardHit = new RaycastHit();
            bool foundDownwardSurface = false;
            float closestDownwardDistance = float.MaxValue;

            foreach (Vector3 downDir in downwardDirections)
            {
                if (Physics.Raycast(downwardOrigin, downDir.normalized, out RaycastHit downHit, raycastDistance * 3f, groundMask))
                {
                    if (downHit.distance < closestDownwardDistance)
                    {
                        closestDownwardDistance = downHit.distance;
                        bestDownwardHit = downHit;
                        foundDownwardSurface = true;
                    }
                }
            }

            if (foundDownwardSurface)
            {
                hasGroundBelow = true;
                downwardSurfaceNormal = bestDownwardHit.normal;
                downwardSurfaceDistance = bestDownwardHit.distance;
                downwardSurfacePoint = bestDownwardHit.point;

                Debug.DrawRay(downwardOrigin, (bestDownwardHit.point - downwardOrigin).normalized * bestDownwardHit.distance, Color.yellow, 0.1f);
            }
            else
            {
                hasGroundBelow = false;
                downwardSurfaceDistance = float.MaxValue;
            }
        }

        private SurfaceInfo GetBestCornerSurface(Vector3 moveDirection, Transform bodyTransform)
        {
            SurfaceInfo bestSurface = new SurfaceInfo();
            float bestScore = 0f;

            for (int i = 0; i < multiDirectionalSurfaces.Length; i++)
            {
                if (!multiDirectionalSurfaces[i].isValid) continue;

                SurfaceInfo surface = multiDirectionalSurfaces[i];
                float score = surface.confidence;

                // Boost score if surface aligns with movement direction
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    float movementAlignment = Vector3.Dot(-surface.direction, moveDirection);
                    if (movementAlignment > 0.3f)
                    {
                        score *= (1f + movementAlignment);
                    }
                }

                // Boost score for surfaces that could support the leg
                float supportScore = Mathf.Max(0.1f, Vector3.Dot(surface.normal, bodyTransform.up));
                score *= supportScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSurface = surface;
                }
            }

            return bestSurface;
        }

        private bool ShouldTakeStep(float distanceToTarget, Vector3 moveDirection, bool isMoving, Transform bodyTransform)
        {
            if (IsStepping || timeSinceLastStep <= minStepInterval) return false;

            float distanceFromHelper = Vector3.Distance(helper.transform.position, lastPlantedPosition);

            bool shouldStep = false;

            if (isMoving && moveDirection.sqrMagnitude > 0.05f)
            {
                // Primary condition: step if moving and distance threshold is met
                if (distanceToTarget > stepThreshold)
                {
                    shouldStep = true;
                }
                // Enhanced backward stepping
                else if (moveDirection.sqrMagnitude > 0.1f)
                {
                    Vector3 helperToLeg = (lastPlantedPosition - helper.transform.position).normalized;
                    float alignment = Vector3.Dot(helperToLeg, moveDirection);
                    if (alignment < -0.5f && distanceToTarget > stepThreshold * 0.7f)
                    {
                        shouldStep = true;
                    }
                }
                // Step if leg is significantly behind in movement direction
                else if (distanceFromHelper > stepThreshold * 1.3f)
                {
                    Vector3 helperToLegDir = (lastPlantedPosition - helper.transform.position).normalized;
                    float movementAlignment = Vector3.Dot(moveDirection, -helperToLegDir);
                    if (movementAlignment > 0.4f)
                    {
                        shouldStep = true;
                    }
                }

                // Enhanced lateral transition stepping
                if (!shouldStep && hasLateralSurface)
                {
                    float lateralMovement = Vector3.Dot(moveDirection, -lateralSurfaceNormal);
                    if (lateralMovement > 0.3f && lateralSurfaceDistance < maxStepDistance * 0.8f)
                    {
                        shouldStep = true;
                    }
                }
            }

            // Emergency: leg is extremely far away
            if (!shouldStep && distanceFromHelper > maxStepDistance * 1.1f)
            {
                shouldStep = true;
            }

            return shouldStep;
        }

        public void BeginStep(bool isSprinting = false)
        {
            stepStart = target.transform.position;
            float distance = Vector3.Distance(stepStart, stepEnd);

            // Increase step speed when sprinting for more responsive leg movement
            float baseSpeed = isSprinting ? 8f : 5f;
            float maxSpeed = isSprinting ? 15f : 10f;

            stepSpeed = Mathf.Lerp(baseSpeed, maxSpeed, distance / maxStepDistance);
            stepProgress = 0f;
            wantsToStep = false;
            isStepCompleted = false;
            timeSinceLastStep = 0f;
        }

        public void ForcePosition(Vector3 position)
        {
            lastPlantedPosition = position;
            target.transform.position = position;
            if (foot != null)
            {
                foot.transform.position = position;
            }
            stepProgress = 1f;
            wantsToStep = false;
            isStepCompleted = true;
            timeSinceLastStep = 0f;
        }

    }

    private class LegStepManager
    {
        private float stepCooldown = 0.08f;
        private float stepCooldownTimer = 0f;

        public void Update(Leg[] legs, Vector3 currentMoveDirection, bool isSprinting = false)
        {
            stepCooldownTimer -= Time.deltaTime;
            int steppingCount = 0;
            foreach (Leg leg in legs)
                if (leg.IsStepping) steppingCount++;

            if (steppingCount >= 2) return;
            float currentStepCooldown = isSprinting ? stepCooldown * 0.7f : stepCooldown;
            if (stepCooldownTimer <= 0f)
            {
                Leg bestCandidate = null;
                float bestScore = 0f;
                foreach (Leg leg in legs)
                {
                    if (leg.WantsToStep && !leg.IsStepping)
                    {
                        float dist = leg.DistanceFromLastPlant;
                        float helperDistance = Vector3.Distance(leg.helper.transform.position, leg.lastPlantedPosition);
                        float urgency = 1f;
                        if (helperDistance > leg.maxStepDistance * 0.6f)
                        {
                            urgency = 3f;
                        }
                        else if (helperDistance > leg.maxStepDistance * 0.4f)
                        {
                            urgency = 2f;
                        }
                        if (leg.hasLateralSurface && currentMoveDirection.sqrMagnitude > 0.01f)
                        {
                            float lateralAlignment = Vector3.Dot(currentMoveDirection, -leg.lateralSurfaceNormal);
                            if (lateralAlignment > 0.3f)
                            {
                                urgency *= 1.8f;
                            }
                        }
                        if (currentMoveDirection.sqrMagnitude > 0.01f)
                        {
                            Vector3 helperToLeg = (leg.lastPlantedPosition - leg.helper.transform.position).normalized;
                            float movementAlignment = Vector3.Dot(currentMoveDirection, -helperToLeg);
                            if (movementAlignment > 0.2f)
                            {
                                urgency *= 1.5f;
                            }
                        }
                        if (isSprinting)
                        {
                            urgency *= 1.3f;
                        }
                        float score = (dist + helperDistance) * urgency;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestCandidate = leg;
                        }
                    }
                }

                if (bestCandidate != null)
                {
                    bestCandidate.BeginStep(isSprinting);
                    stepCooldownTimer = currentStepCooldown;
                }
            }
        }
    }
    [Header("Settings")]
    public float bodyHeightOffset = 0.5f;
    public float raycastDistance = 5f;
    public LayerMask groundMask;
    public float moveSpeed = 2f;
    [Header("Sprint Settings")]
    public float sprintSpeed = 4f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    [Header("Other Settings")]
    public float upAlignSpeed = 5f;

    [Header("Legs")]
    public Leg frontLeft;
    public Leg frontRight;
    public Leg backLeft;
    public Leg backRight;

    [Header("References")]
    public Transform cameraTransform;
    public SpiderCamera spiderCamera; // NEW: Reference to camera script for free cam detection

    private GameObject body;
    private LegStepManager stepManager;
    private Leg[] legs;
    private Vector3 currentUp;
    private Vector3 surfaceNormal;
    private bool isOnSurface = false;
    private bool isMoving = false;
    private bool isSprinting = false;

    private float lastMoveTime = 0f;
    private Vector3 lastMoveDirection = Vector3.zero;
    private float moveStopDelay = 0.3f;

    // Enhanced orientation tracking
    private Vector3 targetBodyUp = Vector3.up;
    private Vector3 smoothedBodyUp = Vector3.up;
    private Vector3 lastValidSurfaceNormal = Vector3.up;
    private float surfaceTransitionProgress = 1f;
    private bool isTransitioning = false;

    public Vector3 CurrentUp => currentUp;
    public Vector3 CurrentSurfaceNormal => surfaceNormal;
    public bool IsOnSurface => isOnSurface;

    void Start()
    {
        body = GameObject.Find("myOwn");
        if (body == null) Debug.LogError("GroundStick: 'myOwn' GameObject not found!");

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // NEW: Auto-find SpiderCamera if not assigned
        if (spiderCamera == null)
        {
            spiderCamera = FindFirstObjectByType<SpiderCamera>();
            if (spiderCamera == null)
            {
                Debug.LogWarning("GroundStick: SpiderCamera reference not found. Free cam mode won't work properly.");
            }
        }

        frontLeft.lastPlantedPosition = frontLeft.target.transform.position;
        frontRight.lastPlantedPosition = frontRight.target.transform.position;
        backLeft.lastPlantedPosition = backLeft.target.transform.position;
        backRight.lastPlantedPosition = backRight.target.transform.position;

        stepManager = new LegStepManager();
        legs = new[] { frontLeft, backRight, frontRight, backLeft };

        currentUp = Vector3.up;
        surfaceNormal = Vector3.up;
        targetBodyUp = Vector3.up;
        smoothedBodyUp = Vector3.up;
        lastValidSurfaceNormal = Vector3.up;
    }

    // Moves the spider to a new position cleanly. Without this, calling
    // transform.position = x on the body alone leaves the legs' lastPlantedPosition
    // values in world space at the old spot — and AdjustBodyHeightAndRotation()
    // would immediately pull the body back toward them. Used by respawn logic.
    public void Teleport(Vector3 position)
    {
        if (body == null) body = GameObject.Find("myOwn");
        if (body == null) return;

        Vector3 offset = position - body.transform.position;
        body.transform.position = position;

        if (legs != null)
        {
            foreach (Leg leg in legs)
                leg.ForcePosition(leg.lastPlantedPosition + offset);
        }
    }

    void Update()
    {
        // Handle sprint input
        isSprinting = Input.GetKey(sprintKey);

        Vector3 movementDirection = GetMoveDirection();
        if (movementDirection.sqrMagnitude > 0.02f)
        {
            isMoving = true;
            lastMoveTime = Time.time;
            lastMoveDirection = movementDirection;
        }
        else if (Time.time - lastMoveTime > moveStopDelay)
        {
            isMoving = false;
        }

        CorrectWaywardLegs();

        // Update legs with enhanced surface detection
        foreach (Leg leg in legs)
            leg.UpdateLeg(groundMask, raycastDistance, movementDirection, currentUp, isMoving, body.transform, isSprinting);

        // Calculate body orientation
        CalculateProperBodyOrientation();

        // In free cam mode, keep the body aligned to the surface but don't yaw to the camera.
        // Otherwise, normal camera-driven rotation.
        if (spiderCamera != null && spiderCamera.IsFreeCamActive)
            MaintainSurfaceAlignmentOnly();
        else
            RotateSpiderToCamera();

        MoveSpider();
        AdjustBodyHeightAndRotation();

        stepManager.Update(legs, movementDirection, isSprinting);
    }


    private void CorrectWaywardLegs()
    {
        foreach (Leg leg in legs)
        {
            float distanceFromHelper = Vector3.Distance(leg.helper.transform.position, leg.lastPlantedPosition);

            if (distanceFromHelper > leg.maxStepDistance * 1.5f && !leg.IsStepping)
            {
                Vector3 correctionTarget = leg.helper.transform.position - currentUp * bodyHeightOffset;

                if (Physics.Raycast(leg.helper.transform.position, -currentUp, out RaycastHit hit, raycastDistance, groundMask))
                {
                    correctionTarget = hit.point;
                }

                leg.ForcePosition(correctionTarget);
            }
        }
    }

    private void CalculateProperBodyOrientation()
    {
        List<Vector3> validSurfaceNormals = new List<Vector3>();
        List<float> surfaceWeights = new List<float>();

        foreach (Leg leg in legs)
        {
            Vector3 legSurfaceNormal = Vector3.up;
            float confidence = 0f;
            bool hasValidSurface = false;

            if (leg.isOnValidSurface)
            {
                legSurfaceNormal = leg.surfaceNormal;
                confidence = 1.0f / (leg.surfaceDistance + 0.1f);
                hasValidSurface = true;
            }
            else if (leg.hasLateralSurface)
            {
                legSurfaceNormal = leg.lateralSurfaceNormal;
                confidence = 0.8f / (leg.lateralSurfaceDistance + 0.1f);
                hasValidSurface = true;
            }
            else if (leg.hasGroundBelow)
            {
                legSurfaceNormal = leg.downwardSurfaceNormal;
                confidence = 0.6f / (leg.downwardSurfaceDistance + 0.1f);
                hasValidSurface = true;
            }

            if (hasValidSurface)
            {
                if (leg.IsStepping)
                {
                    confidence *= 0.5f;
                }

                validSurfaceNormals.Add(legSurfaceNormal);
                surfaceWeights.Add(confidence);
            }
        }

        Vector3 calculatedSurfaceNormal = Vector3.up;

        if (validSurfaceNormals.Count > 0)
        {
            Vector3 weightedNormal = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < validSurfaceNormals.Count; i++)
            {
                weightedNormal += validSurfaceNormals[i] * surfaceWeights[i];
                totalWeight += surfaceWeights[i];
            }

            if (totalWeight > 0f)
            {
                calculatedSurfaceNormal = (weightedNormal / totalWeight).normalized;
            }

            float surfaceChangeAngle = Vector3.Angle(lastValidSurfaceNormal, calculatedSurfaceNormal);

            if (surfaceChangeAngle > 15f && !isTransitioning)
            {
                isTransitioning = true;
                surfaceTransitionProgress = 0f;
            }
            else if (surfaceChangeAngle < 5f && isTransitioning)
            {
                isTransitioning = false;
                surfaceTransitionProgress = 1f;
            }

            lastValidSurfaceNormal = calculatedSurfaceNormal;
        }

        isOnSurface = validSurfaceNormals.Count > 0;

        if (isOnSurface)
        {
            targetBodyUp = calculatedSurfaceNormal;
        }
        else
        {
            targetBodyUp = Vector3.up;
        }

        float orientationSpeed = upAlignSpeed;

        if (isTransitioning)
        {
            surfaceTransitionProgress += Time.deltaTime * (orientationSpeed * 0.8f);
            surfaceTransitionProgress = Mathf.Clamp01(surfaceTransitionProgress);
            orientationSpeed *= 2f;
        }

        if (isMoving)
        {
            orientationSpeed *= 1.5f;

            float bodyUpChange = Vector3.Angle(currentUp, targetBodyUp);
            if (bodyUpChange > 20f)
            {
                orientationSpeed *= 1.8f;
            }
        }

        smoothedBodyUp = Vector3.Slerp(smoothedBodyUp, targetBodyUp, Time.deltaTime * orientationSpeed);
        currentUp = smoothedBodyUp.normalized;
        surfaceNormal = currentUp;

        Vector3 bodyPos = body.transform.position;
        Debug.DrawRay(bodyPos, currentUp * 2f, Color.cyan, 0.1f);
        Debug.DrawRay(bodyPos, targetBodyUp * 2.5f, Color.magenta, 0.1f);
        Debug.DrawRay(bodyPos, calculatedSurfaceNormal * 1.8f, Color.yellow, 0.1f);

        if (isTransitioning)
        {
            Debug.DrawRay(bodyPos + Vector3.right * 0.5f, Vector3.up * surfaceTransitionProgress * 2f, Color.red, 0.1f);
        }
    }

    private Vector3 GetMoveDirection()
    {
        if (cameraTransform == null) return Vector3.zero;

        float vertical = Input.GetAxisRaw("Vertical");
        float horizontal = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(vertical) < 0.3f && Mathf.Abs(horizontal) < 0.3f) return Vector3.zero;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        Vector3 surfaceForward = Vector3.ProjectOnPlane(cameraForward, currentUp).normalized;
        Vector3 surfaceRight = Vector3.ProjectOnPlane(cameraRight, currentUp).normalized;

        if (surfaceForward.sqrMagnitude < 0.1f)
        {
            surfaceForward = Vector3.Cross(currentUp, surfaceRight).normalized;
        }
        if (surfaceRight.sqrMagnitude < 0.1f)
        {
            surfaceRight = Vector3.Cross(surfaceForward, currentUp).normalized;
        }

        Vector3 inputDir = (surfaceForward * vertical + surfaceRight * horizontal).normalized;
        return inputDir;
    }

    private void MoveSpider()
    {
        if (cameraTransform == null || body == null) return;

        Vector3 movementDirection = GetMoveDirection();
        if (movementDirection.sqrMagnitude < 0.01f) return;

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        Vector3 moveDelta = movementDirection * currentSpeed * Time.deltaTime;
        Vector3 currentPos = body.transform.position;
        Vector3 targetPos = currentPos + moveDelta;

        bool canMove = true;
        Vector3 finalPosition = targetPos;

        if (Physics.SphereCast(currentPos, 0.15f, movementDirection, out RaycastHit moveHit, moveDelta.magnitude + 0.1f, groundMask))
        {
            canMove = false;

            Vector3 slideDir = Vector3.ProjectOnPlane(movementDirection, moveHit.normal).normalized;
            Vector3 slideDelta = slideDir * currentSpeed * Time.deltaTime * 0.8f;

            if (slideDelta.sqrMagnitude > 0.01f &&
                !Physics.SphereCast(currentPos, 0.15f, slideDir, out _, slideDelta.magnitude + 0.05f, groundMask))
            {
                finalPosition = currentPos + slideDelta;
                canMove = true;
            }

            if (!canMove)
            {
                Vector3 climbDir = Vector3.Slerp(movementDirection, moveHit.normal, 0.6f).normalized;
                climbDir = Vector3.ProjectOnPlane(climbDir, -currentUp) + currentUp * 0.3f;
                climbDir = climbDir.normalized;

                Vector3 climbDelta = climbDir * currentSpeed * Time.deltaTime * 0.7f;

                if (!Physics.SphereCast(currentPos, 0.12f, climbDir, out _, climbDelta.magnitude + 0.05f, groundMask))
                {
                    finalPosition = currentPos + climbDelta;
                    canMove = true;
                }
            }

            if (!canMove)
            {
                Vector3 stepUpDir = (currentUp * 0.7f + movementDirection * 0.3f).normalized;
                Vector3 stepDelta = stepUpDir * currentSpeed * Time.deltaTime * 0.5f;

                if (!Physics.CheckSphere(currentPos + stepDelta, 0.12f, groundMask))
                {
                    finalPosition = currentPos + stepDelta;
                    canMove = true;
                }
            }
        }

        if (canMove)
        {
            body.transform.position = finalPosition;
        }

        if (isOnSurface)
        {
            ApplySurfaceSticking();
        }
    }

    private void ApplySurfaceSticking()
    {
        Vector3 stickDirection = -currentUp;

        if (Physics.Raycast(body.transform.position, stickDirection, out RaycastHit stickHit,
            bodyHeightOffset * 3f, groundMask))
        {
            float excess = stickHit.distance - bodyHeightOffset;
            if (excess > 0f)
                body.transform.position += stickDirection * excess * 0.8f;
        }
    }

    private void RotateSpiderToCamera()
    {
        if (cameraTransform == null || body == null) return;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 surfaceForward = Vector3.ProjectOnPlane(cameraForward, currentUp).normalized;

        if (surfaceForward.sqrMagnitude < 0.1f)
        {
            Vector3 cameraRight = cameraTransform.right;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(cameraRight, currentUp).normalized;
            surfaceForward = Vector3.Cross(currentUp, surfaceRight).normalized;
        }

        if (surfaceForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(surfaceForward, currentUp);
            float rotationSpeed = 6f;

            if (isMoving && Vector3.Angle(body.transform.up, currentUp) > 10f)
            {
                rotationSpeed *= 1.5f;
            }

            body.transform.rotation = Quaternion.Slerp(body.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    // NEW: Maintain surface alignment without camera rotation
    private void MaintainSurfaceAlignmentOnly()
    {
        if (body == null) return;

        // Keep the spider's forward direction but align up to surface
        Vector3 currentForward = body.transform.forward;
        Vector3 surfaceForward = Vector3.ProjectOnPlane(currentForward, currentUp).normalized;

        if (surfaceForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(surfaceForward, currentUp);
            float rotationSpeed = 3f; // Slower than camera-based rotation

            body.transform.rotation = Quaternion.Slerp(body.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void AdjustBodyHeightAndRotation()
    {
        Vector3 avgFootPos = Vector3.zero;
        float totalWeight = 0f;

        foreach (Leg leg in legs)
        {
            Vector3 footPos = leg.foot != null ? leg.foot.transform.position : leg.target.transform.position;

            float weight = 1f;

            if (leg.isOnValidSurface)
                weight = 1f / (leg.surfaceDistance + 0.1f);
            else if (leg.hasLateralSurface)
                weight = 0.8f / (leg.lateralSurfaceDistance + 0.1f);
            else if (leg.hasGroundBelow)
                weight = 0.6f / (leg.downwardSurfaceDistance + 0.1f);
            else
                weight = 0.3f;

            avgFootPos += footPos * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0f)
        {
            avgFootPos /= totalWeight;
        }

        Vector3 targetPos = avgFootPos + currentUp * bodyHeightOffset;

        float heightAdjustSpeed = 8f;
        if (isMoving && Vector3.Angle(body.transform.up, currentUp) > 15f)
        {
            heightAdjustSpeed *= 1.3f;
        }

        body.transform.position = Vector3.Lerp(body.transform.position, targetPos, Time.deltaTime * heightAdjustSpeed);

        Vector3 bodyForward = Vector3.ProjectOnPlane(body.transform.forward, currentUp).normalized;

        if (bodyForward.sqrMagnitude < 0.01f)
        {
            Vector3 bodyMoveDir = GetMoveDirection();
            if (bodyMoveDir.sqrMagnitude > 0.01f)
            {
                bodyForward = Vector3.ProjectOnPlane(bodyMoveDir, currentUp).normalized;
            }
            else if (cameraTransform != null)
            {
                bodyForward = Vector3.ProjectOnPlane(cameraTransform.forward, currentUp).normalized;
            }

            if (bodyForward.sqrMagnitude < 0.01f)
            {
                Vector3 worldRight = Vector3.Cross(currentUp, Vector3.up).normalized;
                if (worldRight.sqrMagnitude < 0.01f)
                {
                    worldRight = Vector3.Cross(currentUp, Vector3.forward).normalized;
                }
                bodyForward = Vector3.Cross(worldRight, currentUp).normalized;
            }
        }

        if (bodyForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(bodyForward, currentUp);
            float rotSpeed = 5f;

            if (isMoving)
            {
                float orientationChange = Vector3.Angle(body.transform.up, currentUp);
                if (orientationChange > 10f)
                {
                    rotSpeed *= (1f + orientationChange / 45f);
                }
            }

            body.transform.rotation = Quaternion.Slerp(body.transform.rotation, targetRot, Time.deltaTime * rotSpeed);
        }
    }
}