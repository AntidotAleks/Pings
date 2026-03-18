using System;
using UnityEngine;

namespace pings
{
    public enum MessageType 
    {
        PingsModIsPresent =     1001, // Sent by host to indicate Pings mod is present
        PingsModIsRemoved =     1002, // Sent by host when Mod is unloaded
        RequestPingsModStatus = 1003, // Sent by client to check if Pings mod is present
        Ping =                  1004, // Sent by any player to ping a position
    }
    
    public static class Networking
    {
        internal static void OnLoad()
        {
            if (Raft_Network.IsHost)
            {
                Pings.Log("Player is host, Pings mod is active.", 2, "Networking");
                Pings.mod.SendNetworkMessage((Messages)MessageType.PingsModIsPresent);
                Pings.HasPingsMod = true;
            }
            else if (RAPI.IsCurrentSceneGame()) // In a world, but not host
            {
                Pings.Log("[Pings: Networking] Player is not host, requesting Pings mod status.", 2, "Networking");
                Pings.mod.SendNetworkMessage(MessageType.RequestPingsModStatus);
            }
        }

        internal static void OnUnload()
        {
            Pings.HasPingsMod = false;
            if (Raft_Network.IsHost)
                Pings.mod.SendNetworkMessage(MessageType.PingsModIsRemoved);
                // Notify clients that Pings mod is removed
        }

        internal static void CheckMessages(object message)
        {
            switch (message)
            {
                case MessageType.PingsModIsPresent:
                    Pings.Log("Pings mod is enabled on the server.", 2, "Networking");
                    Pings.HasPingsMod = true;
                    break;
                
                
                case MessageType.PingsModIsRemoved:
                    Pings.Log("Pings mod was disabled on the server.", 2, "Networking");
                    Pings.HasPingsMod = false;
                    break;
                
                
                case MessageType.RequestPingsModStatus:
                    Pings.Log("Received request for Pings mod status, responding.", 2, "Networking");
                    Pings.mod.SendNetworkMessage(MessageType.PingsModIsPresent);
                    break;
                
                case PingMessage pingMessage:
                    var senderID = pingMessage.id;
                    if (senderID == Pings.CurrentUserID)
                        return; // Ignore relayed own pings (self -> host -> self)

                    var position = pingMessage.Position;
                    if (Raft_Network.IsHost)
                        Pings.mod.SendNetworkMessage(new PingMessage(position, senderID)); // As host, relay ping to all others (someone -> host-self -> everyone)
                    
                    var hitTransform = CastUtil.ClosestTransform(position); // Find the closest transform to the ping position
                    Pings.Log($"Received a ping packet at {position} from player {RAPI.GetUsernameFromUserID(senderID)}.", 2, "Networking");
                    PingManager.CreatePing(senderID, position, hitTransform);
                    break;
                
                default:
                    Pings.Log($"Unknown message type received: {message.GetType()} ({message})", 2, "Networking");
                    break;
            }
            
        }
    }

    [Serializable]
    public class PingMessage : Message
    {
        public Network_UserId id;
        public float x, y, z;

        // Sending player's SteamID through the message since network messages don't carry it on relay
        public PingMessage(Vector3 position, Network_UserId id)
            : base((Messages)MessageType.Ping)
        {
            x = position.x;
            y = position.y;
            z = position.z;
            this.id = id;
        }

        public Vector3 Position => new Vector3(x, y, z);
    }
}