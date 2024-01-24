using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Netick;
using Netick.Unity;
using KinematicCharacterController;

[ExecuteBefore(typeof(KccPlayer))]
public class ExamplePhysicsMover : NetworkBehaviour, IMoverController
{
    [Networked] public PhysicsMoverState NetworkState { get; set; }
    [Networked] public float Time { get; set; }

    public Vector3 TranslationAxis = Vector3.right;
    public float TranslationPeriod = 10;
    public float TranslationSpeed = 1;
    public Vector3 RotationAxis = Vector3.up;
    public float RotSpeed = 10;
    public Vector3 OscillationAxis = Vector3.zero;
    public float OscillationPeriod = 10;
    public float OscillationSpeed = 10;

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

    public override void NetworkFixedUpdate()
    {
        if (Sandbox == null)
            return;

        Mover.VelocityUpdate(Sandbox.FixedDeltaTime);

        Mover.Transform.SetPositionAndRotation(Mover.TransientPosition, Mover.TransientRotation);
        rb.position = Mover.TransientPosition;
        rb.rotation = Mover.TransientRotation;

        Physics.SyncTransforms();
        if (Sandbox.IsServer)
            NetworkState = Mover.GetState();
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

    public override void NetcodeIntoGameEngine()
    {
        Mover.ApplyState(NetworkState);
    }

    public override void GameEngineIntoNetcode()
    {
        if (Sandbox.IsServer)
            NetworkState = Mover.GetState();
    }
}
