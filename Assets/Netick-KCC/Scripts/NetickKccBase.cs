using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;
using Unity.VisualScripting;

[Networked]
public struct KCCNetworkState
{
    public NetworkBool MustUnground;
    [Networked] public float MustUngroundTime { get; set; }
    public NetworkBool LastMovementIterationFoundAnyGround;

    public NetworkBool FoundAnyGround;
    public NetworkBool IsStableOnGround;
    public NetworkBool SnappingPrevented;
    [Networked] public Vector3 GroundNormal { get; set; }
    [Networked] public Vector3 InnerGroundNormal { get; set; }
    [Networked] public Vector3 OuterGroundNormal { get; set; }
}

[ExecuteBefore(typeof(NetickKccSimulator))]
public class NetickKccBase : NetworkBehaviour
{
    [Networked(relevancy: Relevancy.InputSource)] public KCCNetworkState KCCState { get; set; }
    [Networked][Smooth] public Vector3 Velocity { get; set; }

    protected KinematicCharacterMotorNetick _motor;

    protected void Initialize()    //call this on NetworkStart or NetworkAwake
    {
        TryGetComponent<KinematicCharacterMotorNetick>(out _motor);

        // We disable Settings.AutoSimulation + Settings.Interpolate of KinematicCharacterSystem to essentially handle the simulation ourself
        KinematicCharacterSystem.EnsureCreation();
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;

        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (IsPredicted || Sandbox.IsServer)
            simulator.CharacterMotors.Add(this);
    }

    protected void Cleanup()    //call this on NetworkDestroy
    {
        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (IsPredicted || Sandbox.IsServer)
            simulator.CharacterMotors.Remove(this);
    }

    protected void SetPhysicsScene()
    {
        _motor._PhysicsScene = Sandbox.Physics;
    }

    //rollback client state
    public override void NetcodeIntoGameEngine()
    {
        _motor.ApplyState(NetickStateToKCCState(KCCState));
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

        return kccState;
    }

    //at the end of the tick, set our KCC state
    public override void GameEngineIntoNetcode()
    {
        KCCState = KCCStateToNetickState(_motor.GetState());
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

        return kccNetState;
    }

    protected void Simulate()
    {
        //_motor.UpdatePhase1(Sandbox.FixedDeltaTime);
        //_motor.UpdatePhase2(Sandbox.FixedDeltaTime);
        //_motor.Transform.SetPositionAndRotation(_motor.TransientPosition, _motor.TransientRotation);

        //Velocity = _motor.BaseVelocity;
    }

    public void UpdatePhase1(float deltaTime)
    {
        _motor.UpdatePhase1(deltaTime);
    }

    public void UpdatePhase2(float deltaTime)
    {
        _motor.UpdatePhase2(deltaTime);
        _motor.Transform.SetPositionAndRotation(_motor.TransientPosition, _motor.TransientRotation);
    }

    public virtual void PostSimulate()
    {
        Velocity = _motor.BaseVelocity;
    }
}
