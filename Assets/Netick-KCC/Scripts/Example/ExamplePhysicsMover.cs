using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;

[ExecuteBefore(typeof(NetickKCC), OrderDecrease = 20)]  //we need to ensure this runs before the player so that we can add the platforms interpolation to the player
public class ExamplePhysicsMover : NetworkBehaviour, IMoverController
{
    [Networked]
    public struct PhysicsMoverNetworkedState
    {
        [Networked] public Vector3 Position { get; set; }
        [Networked] public Quaternion Rotation { get; set; }
        [Networked] public Vector3 Velocity { get; set; }
        [Networked] public Vector3 AngularVelocity { get; set; }

        public PhysicsMoverNetworkedState(PhysicsMoverState physicsMoverState)
        {
            Position = physicsMoverState.Position;
            Rotation = physicsMoverState.Rotation;
            Velocity = physicsMoverState.Velocity;
            AngularVelocity = physicsMoverState.AngularVelocity;
        }

        public PhysicsMoverState GetPhysicsMoverState()
        {
            return new PhysicsMoverState() { Position = Position, Rotation = Rotation, Velocity = Velocity, AngularVelocity = AngularVelocity };
        }
    }

    [Networked] [Smooth(false)] public PhysicsMoverNetworkedState NetworkState { get; set; }
    [Networked] public float Time { get; set; }

    public Vector3 TranslationAxis = Vector3.right;
    public float TranslationPeriod = 10;
    public float TranslationSpeed = 1;
    public Vector3 RotationAxis = Vector3.up;
    public float RotSpeed = 10;
    public Vector3 OscillationAxis = Vector3.zero;
    public float OscillationPeriod = 10;
    public float OscillationSpeed = 10;

    private Interpolator transformInterpolator;
    private PhysicsMover Mover;
    private Rigidbody rb;
    private Vector3 initalPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Mover = GetComponent<PhysicsMover>();
        initalPosition = transform.position;
        Mover.MoverController = this;
    }

    public override void NetworkStart()
    {
        transformInterpolator = FindInterpolator(nameof(NetworkState));
        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (IsPredicted || Sandbox.IsServer)
            simulator.PhysicsMovers.Add(Mover);
    }

    public override void NetworkDestroy()
    {
        NetickKccSimulator simulator = NetickKccSimulator.GetSimulator(Sandbox);
        if (IsPredicted || Sandbox.IsServer)
            simulator.PhysicsMovers.Remove(Mover);
    }

    public override void NetworkRender()
    {
        transformInterpolator.GetInterpolationData<PhysicsMoverNetworkedState>(InterpolationSource.Auto, out var transformFrom, out var transformTo, out float alpha);
        //Debug.Log(transformFrom.Position);
        //Debug.Log(transformTo.Position);
        //return;
        transform.GetChild(0).position = Vector3.Lerp(transformFrom.Position, transformTo.Position, alpha);
        transform.GetChild(0).rotation = Quaternion.Lerp(transformFrom.Rotation, transformTo.Rotation, alpha);
        //Mover.RotationDeltaFromInterpolation = Quaternion.Inverse(Mover.LatestInterpolationRotation) * transform.GetChild(0).localRotation;
        Mover.LatestInterpolationRotation = transform.GetChild(0).localRotation;
    }

    public override void NetcodeIntoGameEngine()
    {
        Mover.ApplyState(NetworkState.GetPhysicsMoverState());
    }

    public override void GameEngineIntoNetcode()
    {
        //Physics.SyncTransforms();
        NetworkState = new PhysicsMoverNetworkedState(Mover.GetState());
    }

    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        if (Sandbox == null)
        {
            goalPosition = transform.position;
            goalRotation = transform.rotation;
            return;
        }
            
        Time += deltaTime;

        goalPosition = initalPosition + ((TranslationAxis.normalized * Mathf.Sin(Time * TranslationSpeed) * TranslationPeriod));

        //Quaternion targetRotForOscillation = Quaternion.Euler(OscillationAxis.normalized * (Mathf.Sin(Time.time * OscillationSpeed) * OscillationPeriod));
        goalRotation = Quaternion.Euler(RotationAxis * RotSpeed * Time);// * targetRotForOscillation;
    }
}
