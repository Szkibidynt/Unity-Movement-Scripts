using DG.Tweening;
using UnityEngine;
public class CamMov : MonoBehaviour
{

    [Header("References")]
    public Transform cameraHolder;
    public Transform orientation;
    public Transform tiltHolder;
    private Movement pm;
    private graczuch gr;
    private Camera cam;
    public Camera overlayCam;
    private PlayerInputHandler input;

    [Header("Reszta tam tych zmiennych")]
    [SerializeField] private float defFov =90f;
    public float SensX = 1f;
    public float SensY = 1f;
    public float tiltAngle;
    private float xRotation;
    private float yRotation;
    private float currentTilt;


    private void Start()
    {
        pm = GetComponent<Movement>();
        gr = GetComponent<graczuch>();
        cam = GetComponentInChildren<Camera>();
        defFov = cam.fieldOfView;
        input = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (EscMenu.IsPaused || pm.freeze || gr.isDead)
            return;
        float mouseX = input.LookInput.x * SensX * .1f;
        float mouseY = input.LookInput.y * SensY * .1f;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);

        float horizontalInput = input.MoveInput.x;
        float targetTilt;

        if (pm.state == Movement.MovementState.walking)
        {
            targetTilt = -horizontalInput * tiltAngle;
            DoTilt(targetTilt);
        }
        else if (pm.state != Movement.MovementState.wallrunning)
            DoTilt(0f);

    }

    public void DoFov(float endValue)
    {
        cam.DOKill();
        cam.DOFieldOfView(defFov + endValue, 0.25f);
    }

    public void DoTilt(float zTilt)
    {
        if (tiltHolder == null) return;
        if (Mathf.Approximately(currentTilt, zTilt)) return;

        currentTilt = zTilt;
        tiltHolder.DOKill();
        tiltHolder.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f).SetEase(Ease.OutQuad);
    }
    void LateUpdate()
    {
        if (overlayCam != null)
            overlayCam.fieldOfView = cam.fieldOfView;
    }

}