using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;

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

    //comment these out if u dont need physics movers to work
    [Networked] public Vector3 AttachedRigidbodyVelocity { get; set; }
    public int AttachedRigidbodyNetworkID;  //this is to make moving platforms work.
}

interface IKccPlayerCore
{
    void PostSimulate(); // interface method (does not have a body)
}

[RequireComponent(typeof(NetworkTransform))]
[ExecuteBefore(typeof(NetickKccSimulator))]
public class NetickKCC : NetworkBehaviour
{
    [Networked(relevancy: Relevancy.InputSource)] public KCCNetworkState KCCState { get; set; }
    [Networked][Smooth] public Vector3 BaseVelocity { get; set; }

    [Header("NetickKCC.cs handles rollback of the kcc state and motor simulation.")]
    [Space(10)]
    public bool EnableMovingPlatforms = true;
    public bool RotateWithPhysicsMover = true;

    public KinematicCharacterMotorNetick Motor;
    private IKccPlayerCore _player;

    private void Reset()
    {
        Motor = GetComponent<KinematicCharacterMotorNetick>();
    }

    private void Awake()
    {
        TryGetComponent<IKccPlayerCore>(out _player);
        //_player.SetKCC(this);
    }

    public override void NetworkStart()
    {
        Initialize();
        SetPhysicsScene();
    }

    public override void NetworkDestroy()
    {
        Cleanup();
    }

    protected void Initialize()    //call this on NetworkStart or NetworkAwake
    {
        // We disable Settings.AutoSimulation + Settings.Interpolate of KinematicCharacterSystem to essentially handle the simulation ourself
        KinematicCharacterSystem.EnsureCreation();
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;

        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (Object.PredictionMode == Relevancy.Everyone || IsInputSource || Sandbox.IsServer)
            simulator.CharacterMotors.Add(this);
    }

    protected void Cleanup()    //call this on NetworkDestroy
    {
        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (Object.PredictionMode == Relevancy.Everyone || IsInputSource || Sandbox.IsServer)
            simulator.CharacterMotors.Remove(this);
    }

    protected void SetPhysicsScene()
    {
        Motor._PhysicsScene = Sandbox.Physics;
    }

    public void UpdatePhase1(float deltaTime)
    {
        if (enabled)
            Motor.UpdatePhase1(deltaTime);

        if (RotateWithPhysicsMover && Motor.AttachedRigidbody != null)
        {
            PhysicsMover mover = Motor.AttachedRigidbody.GetComponent<PhysicsMover>();
            //Debug.Log((mover.TransientRotation * Quaternion.Inverse(mover.InitialSimulationRotation)).eulerAngles.y);
            Motor.SetRotation(Motor.transform.rotation * (mover.TransientRotation * Quaternion.Inverse(mover.InitialSimulationRotation)));
        }
    }

    public void UpdatePhase2(float deltaTime)
    {
        if (enabled)
            Motor.UpdatePhase2(deltaTime);
        Motor.Transform.SetPositionAndRotation(Motor.TransientPosition, Motor.TransientRotation);
    }

    public void PostSimulate()
    {
        BaseVelocity = Motor.BaseVelocity;
        _player.PostSimulate();
    }

    //rollback client state
    public override void NetcodeIntoGameEngine()
    {
        Motor.ApplyState(NetickStateToKCCState(KCCState));
    }

    private KinematicCharacterMotorState NetickStateToKCCState(KCCNetworkState kccNetState)
    {
        KinematicCharacterMotorState kccState = new KinematicCharacterMotorState();

        kccState.Position = transform.position;
        kccState.Rotation = transform.rotation;
        kccState.BaseVelocity = BaseVelocity;

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

        kccState.AttachedRigidbodyVelocity = kccNetState.AttachedRigidbodyVelocity;
        if (EnableMovingPlatforms)
        {
            if (kccNetState.AttachedRigidbodyNetworkID != 0)
            {
                if (Sandbox.TryGetObject(kccNetState.AttachedRigidbodyNetworkID, out NetworkObject obj))
                    obj.TryGetComponent<Rigidbody>(out kccState.AttachedRigidbody);
            }
        }

        return kccState;
    }

    //at the end of the tick, set our KCC state
    public override void GameEngineIntoNetcode()
    {
        KCCState = KCCStateToNetickState(Motor.GetState());
    }

    private KCCNetworkState KCCStateToNetickState(KinematicCharacterMotorState state)
    {
        KCCNetworkState kccNetState = new KCCNetworkState();

        transform.position = state.Position;
        transform.rotation = state.Rotation;
        BaseVelocity = state.BaseVelocity;

        kccNetState.MustUnground = state.MustUnground;
        kccNetState.MustUngroundTime = state.MustUngroundTime;
        kccNetState.LastMovementIterationFoundAnyGround = state.LastMovementIterationFoundAnyGround;

        kccNetState.FoundAnyGround = state.GroundingStatus.FoundAnyGround;
        kccNetState.IsStableOnGround = state.GroundingStatus.IsStableOnGround;
        kccNetState.SnappingPrevented = state.GroundingStatus.SnappingPrevented;
        kccNetState.GroundNormal = state.GroundingStatus.GroundNormal;
        kccNetState.InnerGroundNormal = state.GroundingStatus.InnerGroundNormal;
        kccNetState.OuterGroundNormal = state.GroundingStatus.OuterGroundNormal;

        kccNetState.AttachedRigidbodyVelocity = state.AttachedRigidbodyVelocity;
        if (EnableMovingPlatforms)
        {
            kccNetState.AttachedRigidbodyNetworkID = 0;
            if (state.AttachedRigidbody && state.AttachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject obj))
                kccNetState.AttachedRigidbodyNetworkID = obj.Id;
        }

        return kccNetState;
    }
}
