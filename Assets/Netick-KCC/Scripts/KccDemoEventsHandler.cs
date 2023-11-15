using UnityEngine;
using Netick;
using Netick.Unity;

public class KccDemoEventsHandler : NetworkEventsListener
{
    public Transform SpawnPos;
    public GameObject PlayerPrefab;
    public bool SpawnPlayerForHost = false;

    // This is called on the server and the clients when the scene has been loaded.
    public override void OnSceneLoaded(NetworkSandbox sandbox)
    {
        if (sandbox.IsClient)
            return;

        for (int i = 0; i < sandbox.ConnectedPlayers.Count; i++)
        {
            // if SpawnPlayerForHost is set to false, we don't spawn a player for the server
            // index zero is the server player

            if (!SpawnPlayerForHost && i == 0)
                continue;

            var p = sandbox.ConnectedPlayers[i];

            var spawnPos = SpawnPos.position + Vector3.left * (i);
            var player = sandbox.NetworkInstantiate(PlayerPrefab, spawnPos, Quaternion.identity, p).GetComponent<KccPlayer>();
            p.PlayerObject = player.gameObject;
        }
    }

    // This is called on the server when a client has connected.
    public override void OnClientConnected(NetworkSandbox sandbox, NetworkConnection client)
    {
        var spawnPos = SpawnPos.position + Vector3.left * (1 + sandbox.ConnectedPlayers.Count);
        var player = sandbox.NetworkInstantiate(PlayerPrefab, spawnPos, Quaternion.identity, client).GetComponent<KccPlayer>();
        client.PlayerObject = player.gameObject;
    }
}