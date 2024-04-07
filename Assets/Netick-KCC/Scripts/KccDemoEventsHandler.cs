using UnityEngine;
using Netick;
using Netick.Unity;

public class KccDemoEventsHandler : NetworkEventsListener
{
    public Transform SpawnPos;
    public GameObject PlayerPrefab;

    // This is called on the server and the clients when the scene has been loaded.
    public override void OnSceneLoaded(NetworkSandbox sandbox)
    {
        if (sandbox.IsClient)
            return;

        for (int i = 0; i < sandbox.ConnectedPlayers.Count; i++)
        {
            if (sandbox.ConnectedPlayers[i].PlayerObject != null)
                continue;

            var p = sandbox.ConnectedPlayers[i];

            var spawnPos = SpawnPos.position + Vector3.left * (i);
            var player = sandbox.NetworkInstantiate(PlayerPrefab, spawnPos, Quaternion.identity, p);
            p.PlayerObject = player.gameObject;
        }
    }

    // This is called on the server when a client has connected.
    public override void OnPlayerConnected(NetworkSandbox sandbox, Netick.NetworkPlayer client)
    {
        var spawnPos = SpawnPos.position + Vector3.left * (1 + sandbox.ConnectedPlayers.Count);
        var player = sandbox.NetworkInstantiate(PlayerPrefab, spawnPos, Quaternion.identity, client);
        client.PlayerObject = player.gameObject;
    }
}