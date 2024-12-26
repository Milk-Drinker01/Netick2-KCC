using UnityEngine;
using Netick;
using Netick.Unity;

public struct KccDemoInput : INetworkInput
{
    public Vector2 YawPitchDelta;
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
    
    //[Networked] [Smooth] public Vector2 YawPitch { get; set; }
    [Networked] [Smooth] public float Pitch { get; set; }
    
    [Networked] public NetworkBool Crouching { get; set; }

    private Locomotion _locomotion;

    private Interpolator rotationInterpolator;
    private Vector2 _camAngles;
    private bool _crouching;


    private void Awake()
    {
        Initialize();
        TryGetComponent<Locomotion>(out _locomotion);
    }

    public override void NetworkStart()
    {
        //rotationInterpolator = FindInterpolator(nameof(YawPitch));
        TryGetComponent<NetworkTransform>(out NetworkTransform netTransform);
        rotationInterpolator = netTransform.FindInterpolator(nameof(netTransform.Rotation));
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
        //if (IsProxy)    //on local client, we apply the camera rotation using the values set in NetworkUpdate. on proxies, we use the interpolated values
        //{
        //    //rotationInterpolator.GetInterpolationData<Vector2>(InterpolationSource.Auto, out var rotationFrom, out var rotationTo, out float alpha);
        //    rotationInterpolator.GetInterpolationData<Quaternion>(InterpolationSource.Auto, out var rotationFrom, out var rotationTo, out float alpha);
        //    //GetComponent<NetworkTransform>().Rotation;
        //    ApplyRotations(LerpRotation(rotationFrom.eulerAngles.y, rotationTo.eulerAngles.y, alpha), Pitch);
        //    ApplyRotations(GetComponent<NetworkTransform>().Rotation.eulerAngles.y, Pitch);
        //}

        if (IsInputSource)
            ApplyRotations(_camAngles);

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
        networkInput.YawPitchDelta += camInput;
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

        //on the local client, we set the rotation to be applied during NetworkRender during NetworkUpdate,
        //rather than using the interpolated value, to prevent look delay
        _camAngles = ClampAngles(_camAngles.x + camInput.x, _camAngles.y + camInput.y);
        //ApplyRotations(_camAngles);
    }

    public override void NetworkFixedUpdate()
    {
        if (FetchInput(out KccDemoInput input))
        {
            Pitch = Mathf.Clamp(Pitch + input.YawPitchDelta.y, -90, 90);
            //YawPitch = ClampAngles(YawPitch + input.YawPitch);
            LocomotionInputs characterInputs = new LocomotionInputs { 
                MoveAxisForward  = input.Movement.y,
                MoveAxisRight = input.Movement.x,
                Sprint = (input.Sprint && input.Movement.y > 0),
                //ForwardVector = Quaternion.Euler(0, YawPitch.x, 0),
                ForwardVector = Quaternion.Euler(0, transform.eulerAngles.y + input.YawPitchDelta.x, 0),
                JumpDown = input.JumpDown,
            };

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

        //ApplyRotations(YawPitch);
    }

    private void ApplyRotations(Vector2 camAngles)
    {
        ApplyRotations(camAngles.x, camAngles.y);
        _camAngles = camAngles;
    }

    private void ApplyRotations(float yaw, float pitch)
    {
        RenderTransform.rotation = Quaternion.Euler(0, yaw, 0);
        CameraTransform.localRotation = Quaternion.Euler(pitch, 0, 0);
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
