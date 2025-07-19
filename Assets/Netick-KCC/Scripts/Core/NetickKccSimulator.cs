using KinematicCharacterController;
using Netick.Unity;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NetickKccSimulator : NetickBehaviour
{
    public List<NetickKCC> CharacterMotors = new List<NetickKCC>();
    public List<PhysicsMover> PhysicsMovers = new List<PhysicsMover>();

    public override void NetworkFixedUpdate()
    {
        Simulate(Sandbox.ScaledFixedDeltaTime, CharacterMotors, PhysicsMovers);
    }

    public static void Simulate(float deltaTime, List<NetickKCC> motors, List<PhysicsMover> movers)
    {
        int characterMotorsCount = motors.Count;
        int physicsMoversCount = movers.Count;

#pragma warning disable 0162
        Physics.SyncTransforms();
        // Update PhysicsMover velocities
        for (int i = 0; i < physicsMoversCount; i++)
        {
            movers[i].VelocityUpdate(deltaTime);
        }

        // Character controller update phase 1
        for (int i = 0; i < characterMotorsCount; i++)
        {
            motors[i].UpdatePhase1(deltaTime);
        }

        // Simulate PhysicsMover displacement
        for (int i = 0; i < physicsMoversCount; i++)
        {
            PhysicsMover mover = movers[i];

            mover.Transform.SetPositionAndRotation(mover.TransientPosition, mover.TransientRotation);
            mover.Rigidbody.position = mover.TransientPosition;
            mover.Rigidbody.rotation = mover.TransientRotation;
        }
        //Physics.SyncTransforms();

        // Character controller update phase 2 and move
        for (int i = 0; i < characterMotorsCount; i++)
        {
            NetickKCC motor = motors[i];

            motor.UpdatePhase2(deltaTime);

            motor.PostSimulate();
        }
        Physics.SyncTransforms();
#pragma warning restore 0162
    }

    public static NetickKccSimulator GetSimulator(NetworkSandbox Sandbox)
    {
        if (!Sandbox.TryGetComponent<NetickKccSimulator>(out NetickKccSimulator simulator))
        {
            simulator = Sandbox.AddComponent<NetickKccSimulator>();
            Sandbox.AttachBehaviour(simulator);
        }
        return simulator;
    }
}
