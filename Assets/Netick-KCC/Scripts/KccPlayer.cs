using KinematicCharacterController;
using Netick;
using Netick.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public struct KccDemoInput : INetworkInput
{
    public Vector2 YawPitch;
    public Vector2 Movement;
    public bool Sprint;
    public bool CouchInput;
    public bool JumpDown;
}

public struct KCCNetworkState
{
    public NetworkBool MustUnground;
    public float MustUngroundTime;
    public NetworkBool LastMovementIterationFoundAnyGround;

    public NetworkBool FoundAnyGround;
    public NetworkBool IsStableOnGround;
    public NetworkBool SnappingPrevented;
    public Vector3 GroundNormal;
    public Vector3 InnerGroundNormal;
    public Vector3 OuterGroundNormal;
}

//put things that you dont really give a fuck about being networked here
//but need to be rolled back in the simulation
public struct AdditionalKCCNetworkInfo
{
    public bool JumpConsumed;
    public bool JumpedThisFrame;
    public Rigidbody AttachedRigidbody;
    public Vector3 AttachedRigidbodyVelocity;
}

public class KccPlayer : NetworkBehaviour
{
    [SerializeField] private float _sensitivityX = 1f;
    [SerializeField] private float _sensitivityY = 1f;
    [SerializeField] private bool ToggleCrouch = false;

    [SerializeField] private Transform RenderTransform;
    [SerializeField] private Transform CameraTransform;


    [Networked(relevancy: Relevancy.InputSource)] public KCCNetworkState KCCState { get; set; }
    private AdditionalKCCNetworkInfo[] AdditionalStateInfoBuffer;


    [Networked] [Smooth] public Vector3 Velocity { get; set; }  //we exclude velocity from the state struct cause we might want smoothed velocity for animation purposes
    [Networked] [Smooth] public Vector2 YawPitch { get; set; }

    private Interpolator rotationInterpolator;
    [Networked] public NetworkBool Crouching { get; set; }

    private KinematicCharacterMotorNetick _motor;
    private Locomotion _locomotion;
    private bool _crouching;

    private void Awake()
    {
        _motor = GetComponent<KinematicCharacterMotorNetick>();
        _locomotion = GetComponent<Locomotion>();
    }

    void Start()
    {
        // We disable Settings.AutoSimulation + Settings.Interpolate of KinematicCharacterSystem to essentially handle the simulation ourself
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;
    }

    public override void NetworkStart()
    {
        rotationInterpolator = FindInterpolator(nameof(YawPitch));
        _motor._PhysicsScene = Sandbox.Physics;

        if (IsPredicted)
            InitInfoBuffer();

        if (!IsInputSource)
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
    }

    void InitInfoBuffer()
    {
        AdditionalStateInfoBuffer = new AdditionalKCCNetworkInfo[Sandbox.Config.MaxPredictedTicks];
        for (int i = 0; i < AdditionalStateInfoBuffer.Length; i++)
            AdditionalStateInfoBuffer[i] = new AdditionalKCCNetworkInfo();
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
        bool didGetData = rotationInterpolator.GetInterpolationData<Vector2>(InterpolationMode.Auto, out var rotationFrom, out var rotationTo, out float alpha);
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

        Sandbox.SetInput<KccDemoInput>(networkInput);
    }

    public override void NetcodeIntoGameEngine()
    {
        _motor.ApplyState(NetickStateToKCCState(KCCState));
        if (!IsPredicted)
            return;
        _locomotion.SetLocomotionState(AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length]);
    }

    public override void GameEngineIntoNetcode()
    {
        KCCState = KCCStateToNetickState(_motor.GetState());
        if (!IsPredicted)
            return;
        //InfoBuffer[Sandbox.Tick.TickValue % InfoBuffer.Length] = _locomotion.GetLocomotionState();
        _locomotion.GetLocomotionState(ref AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length]);
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
            Velocity = _motor.Velocity;
        }
    }

    public void Simulate()
    {
        _motor.UpdatePhase1(Sandbox.FixedDeltaTime);
        _motor.UpdatePhase2(Sandbox.FixedDeltaTime);
        _motor.Transform.SetPositionAndRotation(_motor.TransientPosition, _motor.TransientRotation);
    }

    private KCCNetworkState KCCStateToNetickState(KinematicCharacterMotorState state)
    {
        KCCNetworkState kccNetState = new KCCNetworkState();

        transform.position = state.Position;
        transform.rotation = state.Rotation;
        Velocity = state.BaseVelocity;

        kccNetState.MustUnground = state.MustUnground;
        kccNetState.MustUngroundTime = state.MustUngroundTime;
        kccNetState.LastMovementIterationFoundAnyGround = state.LastMovementIterationFoundAnyGround;

        kccNetState.FoundAnyGround = state.GroundingStatus.FoundAnyGround;
        kccNetState.IsStableOnGround = state.GroundingStatus.IsStableOnGround;
        kccNetState.SnappingPrevented = state.GroundingStatus.SnappingPrevented;
        kccNetState.GroundNormal = state.GroundingStatus.GroundNormal;
        kccNetState.InnerGroundNormal = state.GroundingStatus.InnerGroundNormal;
        kccNetState.OuterGroundNormal = state.GroundingStatus.OuterGroundNormal;

        if (IsPredicted)
        {
            if (AdditionalStateInfoBuffer == null)
                InitInfoBuffer();
            AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length].AttachedRigidbody = state.AttachedRigidbody;
            AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length].AttachedRigidbodyVelocity = state.AttachedRigidbodyVelocity;
        }

        return kccNetState;
    }

    private KinematicCharacterMotorState NetickStateToKCCState(KCCNetworkState kccNetState)
    {
        KinematicCharacterMotorState kccState = new KinematicCharacterMotorState();

        kccState.Position = transform.position;
        kccState.Rotation = transform.rotation;
        kccState.BaseVelocity = Velocity;

        kccState.MustUnground = kccNetState.MustUnground;
        kccState.MustUngroundTime = kccNetState.MustUngroundTime;
        kccState.LastMovementIterationFoundAnyGround = kccNetState.LastMovementIterationFoundAnyGround;

        kccState.GroundingStatus = new CharacterTransientGroundingReport()
        {
            FoundAnyGround = kccNetState.FoundAnyGround,
            IsStableOnGround = kccNetState.IsStableOnGround,
            SnappingPrevented = kccNetState.SnappingPrevented,
            GroundNormal = kccNetState.GroundNormal,
            InnerGroundNormal = kccNetState.InnerGroundNormal,
            OuterGroundNormal = kccNetState.OuterGroundNormal
        };

        if (IsPredicted)
        {
            if (AdditionalStateInfoBuffer == null)
                InitInfoBuffer();
            kccState.AttachedRigidbody = AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length].AttachedRigidbody;
            kccState.AttachedRigidbodyVelocity = AdditionalStateInfoBuffer[Sandbox.Tick.TickValue % AdditionalStateInfoBuffer.Length].AttachedRigidbodyVelocity;
        }

        return kccState;
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
