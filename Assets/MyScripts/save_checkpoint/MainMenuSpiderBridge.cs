using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the dancing spider in the Main Menu.
/// The pointing leg is computed every frame by projecting the hovered
/// button's screen rect back into world space at the spider's depth, so
/// it always lands on the actual button regardless of UI scale or window size.
/// </summary>
public class MainMenuSpiderBridge : MonoBehaviour
{
    [Header("References")]
    public MainMenuUIToolkit menu;
    public Camera renderCamera;
    public Transform body;

    [Header("Dance")]
    public float bounceHeight  = 0.15f;
    public float bounceSpeed   = 2f;
    public float bodyTurnSpeed = 5f;

    [Header("Pointing leg")]
    public Transform legTarget;
    [Tooltip("Drag any GameObject here — the leg rests at this Transform's world position.")]
    public Transform legRestAnchor;
    public float legMoveSpeed = 5f;

    [Header("Frozen legs (optional)")]
    public Transform legTarget2;
    public Transform legTarget3;
    public Transform legTarget4;

    [Header("Disco light")]
    public DiscoLight discoLight;

    private MainMenuUIToolkit.MenuButton? _hoveredBtn;
    private Vector3    _bodyStartPos;
    private Quaternion _bodyStartRot;
    private Vector3    _frozen2, _frozen3, _frozen4;
    private Vector3    _restWorldPos;

    private void Start()
    {
        if (renderCamera == null) renderCamera = Camera.main;

        if (body != null)
        {
            _bodyStartPos = body.position;
            _bodyStartRot = body.rotation;
        }

        if (legRestAnchor != null)         _restWorldPos = legRestAnchor.position;
        else if (legTarget != null)        _restWorldPos = legTarget.position;

        if (legTarget2 != null) _frozen2 = legTarget2.position;
        if (legTarget3 != null) _frozen3 = legTarget3.position;
        if (legTarget4 != null) _frozen4 = legTarget4.position;

        if (discoLight != null) discoLight.SetQuitMode(false);
    }

    private void OnEnable()
    {
        if (menu == null) menu = FindFirstObjectByType<MainMenuUIToolkit>(FindObjectsInactive.Include);
        if (menu == null) return;
        menu.HoverEnter += OnHoverEnter;
        menu.HoverExit  += OnHoverExit;
    }

    private void OnDisable()
    {
        if (menu == null) return;
        menu.HoverEnter -= OnHoverEnter;
        menu.HoverExit  -= OnHoverExit;
    }

    private void OnHoverEnter(MainMenuUIToolkit.MenuButton btn)
    {
        _hoveredBtn = btn;
        if (discoLight != null)
            discoLight.SetQuitMode(btn == MainMenuUIToolkit.MenuButton.Quit);
    }

    private void OnHoverExit(MainMenuUIToolkit.MenuButton _)
    {
        _hoveredBtn = null;
        if (discoLight != null) discoLight.SetQuitMode(false);
    }

    private void LateUpdate()
    {
        UpdateLegs();
        UpdateBody();
    }

    private void UpdateLegs()
    {
        bool hovering = false;
        Vector3 hoverTarget = Vector3.zero;

        if (_hoveredBtn.HasValue && menu != null && renderCamera != null)
        {
            Button btn = menu.GetButton(_hoveredBtn.Value);
            if (btn != null && TryProjectElementToWorld(btn, out hoverTarget))
                hovering = true;
        }

        if (legTarget != null)
        {
            if (hovering)
                legTarget.position = Vector3.Lerp(legTarget.position, hoverTarget,
                                                  Time.deltaTime * legMoveSpeed);
            else
                legTarget.position = _restWorldPos;
        }

        if (legTarget2 != null) legTarget2.position = _frozen2;
        if (legTarget3 != null) legTarget3.position = _frozen3;
        if (legTarget4 != null) legTarget4.position = _frozen4;
    }

    private bool TryProjectElementToWorld(VisualElement el, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        var panel = el.panel;
        if (panel == null) return false;

        Rect panelRect = panel.visualTree.worldBound;
        if (panelRect.width <= 0f || panelRect.height <= 0f) return false;

        Rect btnRect = el.worldBound;
        if (btnRect.width <= 0f || btnRect.height <= 0f) return false;

        Vector2 center = btnRect.center;
        float sx = (center.x / panelRect.width) * Screen.width;
        float sy = (1f - center.y / panelRect.height) * Screen.height;

        float depth = body != null ? renderCamera.WorldToScreenPoint(body.position).z : 5f;
        if (depth <= 0f) return false;

        worldPos = renderCamera.ScreenToWorldPoint(new Vector3(sx, sy, depth));
        return true;
    }

    private void UpdateBody()
    {
        if (body == null) return;

        bool lookingAtCamera = _hoveredBtn == MainMenuUIToolkit.MenuButton.Quit;

        if (lookingAtCamera && renderCamera != null)
        {
            Vector3 dir = (renderCamera.transform.position - body.position).normalized;
            Quaternion target = Quaternion.LookRotation(dir);
            float shake = Mathf.Sin(Time.time * 10f) * 3f;
            target *= Quaternion.Euler(0f, shake, 0f);
            body.rotation = Quaternion.Slerp(body.rotation, target,
                                             Time.deltaTime * bodyTurnSpeed);
            body.position = _bodyStartPos;
        }
        else
        {
            float t = Time.time * bounceSpeed;
            float x = Mathf.Sin(t) * 0.2f;
            float y = -(1f - Mathf.Abs(Mathf.Sin(t))) * bounceHeight;
            body.position = _bodyStartPos + new Vector3(x, y, 0f);

            float tiltZ = Mathf.Sin(t) * 12f;
            float tiltX = Mathf.Cos(t * 2f) * 6f;
            body.rotation = _bodyStartRot * Quaternion.Euler(tiltX, 0f, tiltZ);
        }
    }
}
