using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;

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

public class NetickKccBase : NetworkBehaviour
{
    [Networked(relevancy: Relevancy.InputSource)] public KCCNetworkState KCCState { get; set; }
    [Networked] [Smooth] public Vector3 Velocity { get; set; }

    protected KinematicCharacterMotorNetick _motor;

    protected void Initialize()
    {
        TryGetComponent<KinematicCharacterMotorNetick>(out _motor);

        // We disable Settings.AutoSimulation + Settings.Interpolate of KinematicCharacterSystem to essentially handle the simulation ourself
        KinematicCharacterSystem.EnsureCreation();
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;
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
        _motor.UpdatePhase1(Sandbox.FixedDeltaTime);
        _motor.UpdatePhase2(Sandbox.FixedDeltaTime);
        _motor.Transform.SetPositionAndRotation(_motor.TransientPosition, _motor.TransientRotation);

        Velocity = _motor.BaseVelocity;
    }
}
