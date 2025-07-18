using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;

public struct KccDemoInput : INetworkInput
{
    public Vector2 YawPitchDelta;
    public Vector2 Movement;
    public bool Sprint;
    public bool CouchInput;
    public bool JumpDown;
}

[RequireComponent(typeof(NetickKCC))]
[ExecuteBefore(typeof(NetickKCC))]
public class ExamplePlayer : NetworkBehaviour, IKccPlayerCore
{
    [SerializeField] private NetickKCC KCC;
    [SerializeField] private float _sensitivityX = 1f;
    [SerializeField] private float _sensitivityY = 1f;
    [SerializeField] private bool ToggleCrouch = false;

    [SerializeField] private Transform RenderTransform;
    [SerializeField] private Transform CameraTransform;

    [Networked][Smooth] public float Pitch { get; set; }

    [Networked] public NetworkBool Crouching { get; set; }

    private Locomotion _locomotion;

    private Vector2 _camAngles;
    private bool _crouching;

    private void Awake()
    {
        TryGetComponent<Locomotion>(out _locomotion);
    }

    public override void NetworkStart()
    {
        if (!IsInputSource)
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
    }

    public override void OnInputSourceLeft()
    {
        Sandbox.Destroy(Object);
    }

    public override void NetworkRender()
    {
        if (IsInputSource)  //on local client, we apply the camera rotation using the values set in NetworkUpdate. on proxies, we use the interpolated values
        {
            ApplyRotations(_camAngles);
            if (KCC.RotateWithPhysicsMover && KCC.Motor.AttachedRigidbody != null) //apply moving platform rotation interpolation
            {
                PhysicsMover mover = KCC.Motor.AttachedRigidbody.GetComponent<PhysicsMover>();
                RenderTransform.rotation *= mover.LatestInterpolationRotation;
                //Quaternion newRot = (RenderTransform.rotation * mover.LatestInterpolationRotation) * Quaternion.Inverse(RenderTransform.rotation);
                //Debug.Log(newRot.eulerAngles.y);
            }
        }

        float height = Crouching ? _locomotion.CrouchedCapsuleHeight : _locomotion.CapsuleStandHeight;
        RenderTransform.localScale = new Vector3(1, height / 2, 1);
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
            Vector2 movementVector = Vector2.ClampMagnitude(input.Movement, 1);
            LocomotionInputs characterInputs = new LocomotionInputs
            {
                MoveAxisForward = movementVector.y,
                MoveAxisRight = movementVector.x,
                Sprint = (input.Sprint && movementVector.y > 0),
                //ForwardVector = Quaternion.Euler(0, YawPitch.x, 0),
                DeltaCharacterRotation = Quaternion.Euler(0, input.YawPitchDelta.x, 0),
                JumpDown = input.JumpDown,
            };

            if (!Crouching && input.CouchInput)
                characterInputs.CrouchDown = true;
            if (Crouching && !input.CouchInput)
                characterInputs.CrouchUp = true;

            Crouching = input.CouchInput;

            _locomotion.SetInputs(ref characterInputs);
        }
    }

    public void PostSimulate()
    {
        ApplyRotations(new Vector2(transform.eulerAngles.y, Pitch));
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
