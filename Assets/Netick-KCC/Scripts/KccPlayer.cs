using UnityEngine;
using Netick;
using Netick.Unity;

public struct KccDemoInput : INetworkInput
{
    public Vector2 YawPitch;
    public Vector2 Movement;
    public bool Sprint;
    public bool CouchInput;
    public bool JumpDown;
}

public class KccPlayer : NetickKccBase
{
    [SerializeField] private float _sensitivityX = 1f;
    [SerializeField] private float _sensitivityY = 1f;
    [SerializeField] private bool ToggleCrouch = false;

    [SerializeField] private Transform RenderTransform;
    [SerializeField] private Transform CameraTransform;
    
    [Networked] [Smooth] public Vector2 YawPitch { get; set; }

    private Interpolator rotationInterpolator;
    [Networked] public NetworkBool Crouching { get; set; }

    private Locomotion _locomotion;

    private bool _crouching;


    private void Awake()
    {
        Initialize();
        TryGetComponent<Locomotion>(out _locomotion);
    }

    public override void NetworkStart()
    {
        rotationInterpolator = FindInterpolator(nameof(YawPitch));
        SetPhysicsScene();

        if (!IsInputSource)
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
    }

    public delegate void DestroyPlayer();
    public event DestroyPlayer OnPlayerDestroyed;
    public override void OnInputSourceLeft()
    {
        OnPlayerDestroyed?.Invoke();
        Sandbox.Destroy(Object);
    }

    public override void NetworkRender()
    {
        //RenderTransform.position = Position;
        bool didGetData = rotationInterpolator.GetInterpolationData<Vector2>(InterpolationSource.Auto, out var rotationFrom, out var rotationTo, out float alpha);
        RenderTransform.rotation = Quaternion.Euler(0, LerpRotation(rotationFrom.x, rotationTo.x, alpha), 0);
        //RenderTransform.localRotation = Quaternion.Euler(0, YawPitch.x, 0);
        CameraTransform.localRotation = Quaternion.Euler(YawPitch.y, 0, 0);
        float height = Crouching ? _locomotion.CrouchedCapsuleHeight : _locomotion.CapsuleStandHeight;
        RenderTransform.localScale = new Vector3(1, height/2, 1);
    }

    private float LerpRotation(float from, float to, float alpha)
    {
        return Mathf.LerpAngle(from, to, alpha);
    }

    public override void NetworkUpdate()
    {
        if (!IsInputSource || !Sandbox.InputEnabled)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        #region poll input
        var camInput = new Vector2(Input.GetAxisRaw("Mouse X") * _sensitivityX, Input.GetAxisRaw("Mouse Y") * -_sensitivityY);
        camInput *= (Cursor.lockState == CursorLockMode.Locked ? 1 : 0);

        var networkInput = Sandbox.GetInput<KccDemoInput>();
        networkInput.YawPitch += camInput;
        networkInput.Movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        networkInput.Sprint = Input.GetKey(KeyCode.LeftShift);

        networkInput.JumpDown |= Input.GetKeyDown(KeyCode.Space);

        if (ToggleCrouch)
        {
            if (Input.GetKeyDown(KeyCode.C))
                _crouching = !_crouching;
        }
        else
            _crouching = Input.GetKey(KeyCode.C);

        networkInput.CouchInput = _crouching;
        #endregion

        Sandbox.SetInput<KccDemoInput>(networkInput);
    }

    public override void NetworkFixedUpdate()
    {
        if (FetchInput(out KccDemoInput input))
        {
            YawPitch = ClampAngles(YawPitch + input.YawPitch);
            LocomotionInputs characterInputs = new LocomotionInputs();
            characterInputs.MoveAxisForward = input.Movement.y;
            characterInputs.MoveAxisRight = input.Movement.x;
            characterInputs.sprint = input.Sprint && characterInputs.MoveAxisForward > 0;
            characterInputs.CameraRotation = Quaternion.Euler(0, YawPitch.x, 0);

            characterInputs.JumpDown = input.JumpDown;

            if (!Crouching && input.CouchInput)
                characterInputs.CrouchDown = true;
            if (Crouching && !input.CouchInput)
                characterInputs.CrouchUp = true;

            Crouching = input.CouchInput;

            _locomotion.SetInputs(ref characterInputs);
        }

        if (Sandbox.IsServer || IsPredicted)
        {
            Simulate();
        }
    }

    private Vector2 ClampAngles(Vector2 _yawPitch)
    {
        _yawPitch.x = ClampAngle(_yawPitch.x, -360, 360);
        _yawPitch.y = ClampAngle(_yawPitch.y, -90, 90);
        return _yawPitch;
    }

    private Vector2 ClampAngles(float yaw, float pitch)
    {
        return new Vector2(ClampAngle(yaw, -360, 360), ClampAngle(pitch, -90, 90));
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle <= -360F)
            angle += 360F;
        if (angle >= 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
