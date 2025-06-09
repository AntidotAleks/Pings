using System;
using Steamworks;
using UnityEngine;

namespace pings
{
    public enum MessageTypes 
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
                if (Pings.DebugMode >= 2)
                    Debug.Log("[Pings: Networking] Player is host, Pings mod is active.");
                RAPI.SendNetworkMessage(new Message((Messages)MessageTypes.PingsModIsPresent), Pings.ModChannel);
                Pings.HasPingsMod = true;
            }
            else if (RAPI.IsCurrentSceneGame()) // In a world, but not host
            {
                if (Pings.DebugMode >= 2)
                    Debug.Log("[Pings: Networking] Player is not host, requesting Pings mod status.");
                RAPI.SendNetworkMessage(new Message((Messages)MessageTypes.RequestPingsModStatus), Pings.ModChannel);
            }
        }

        internal static void OnUnload()
        {
            Pings.HasPingsMod = false;
            if (Raft_Network.IsHost)
                RAPI.SendNetworkMessage(new Message((Messages)MessageTypes.PingsModIsRemoved), Pings.ModChannel);
                // Notify clients that Pings mod is removed
        }

        internal static void CheckMessages()
        {
            #region Is message received
            var netMessage = RAPI.ListenForNetworkMessagesOnChannel(Pings.ModChannel);
            if (netMessage == null) return;
            var message = netMessage.message;
            #endregion
            switch (message.Type)
            {
                case (Messages)MessageTypes.PingsModIsPresent:
                    
                    if (Pings.DebugMode >= 2)
                        Debug.Log("[Pings: Networking] Pings mod is enabled on the server.");
                    Pings.HasPingsMod = true;
                    break;
                
                
                case (Messages)MessageTypes.PingsModIsRemoved:
                    if (Pings.DebugMode >= 2)
                        Debug.Log("[Pings: Networking] Pings mod was disabled on the server.");
                    Pings.HasPingsMod = false;
                    break;
                
                
                case (Messages)MessageTypes.RequestPingsModStatus:
                    if (Pings.DebugMode >= 2)
                        Debug.Log("[Pings: Networking] Received request for Pings mod status, responding...");
                    RAPI.SendNetworkMessage(new Message((Messages)MessageTypes.PingsModIsPresent), Pings.ModChannel);
                    break;
                
                
                case (Messages)MessageTypes.Ping:
                    if (!(message is PingMessage pingMessage))
                        break; // Ensure the message is of type PingMessage
                    
                    var senderSteamID = pingMessage.steamID;
                    if (senderSteamID == Pings.SteamID)
                        return;
                        // Ignore relayed own pings (self -> host -> self)

                    var position = pingMessage.Position();
                    if (Raft_Network.IsHost)
                        RAPI.SendNetworkMessage(new PingMessage(position, senderSteamID), Pings.ModChannel); 
                        // As host, relay ping to all others (someone -> host-self -> everyone)
                    
                    var hitTransform = CastUtil.ClosestTransform(position); // Find the closest transform to the ping position
                    if (Pings.DebugMode >= 2)
                        Debug.Log($"[Pings: Networking] Received a ping packet at {position} from player {RAPI.GetUsernameFromSteamID(senderSteamID)}.");
                    PingManager.CreatePing(senderSteamID, position, hitTransform);
                    break;
                
                
                default:
                    if (Pings.DebugMode >= 1)
                        Debug.Log($"[Pings: Networking] Unknown message type received: {netMessage.message.Type}. Is another mod using the same channel ({Pings.ModChannel})?");
                    break;
            }
        }
        
    }

    [Serializable]
    public class PingMessage : Message
    {
        public string positionStr;

        public CSteamID steamID;

        // Sending player's SteamID through the message since network messages don't carry it on relay
        public PingMessage(Vector3 position, CSteamID steamID)
            : base((Messages)MessageTypes.Ping)
        {
            positionStr = position.x + "|" + position.y + "|" + position.z; // Serialize position as a string
            this.steamID = steamID;
        }

        public Vector3 Position()
        {
            if (string.IsNullOrEmpty(positionStr)) return Vector3.zero;

            var parts = positionStr.Split('|');
            if (parts.Length == 3 &&
                float.TryParse(parts[0], out var x) &&
                float.TryParse(parts[1], out var y) &&
                float.TryParse(parts[2], out var z)
            )
                return new Vector3(x, y, z);

            return Vector3.zero;
        }
    }
}