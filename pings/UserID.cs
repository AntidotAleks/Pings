using System;

namespace pings
{
    public class UserID
    {
        private readonly ulong _value;
        
        public UserID(object ID)
        {
            string typeName = ID.GetType().FullName;
            
            switch (typeName)
            {
                case "CSteamID": _value = (ulong)ID.GetType().GetField("m_SteamID").GetValue(ID); break;
                case "Network_UserId": _value = (ulong)ID.GetType().GetField("Id").GetValue(ID); break;
                default:
                    throw new ArgumentException($"Unsupported ID type: {typeName}");
            }
        }
        public string GetUsername()
        {
            var raftNetwork = ComponentManager<Raft_Network>.Value;
            return raftNetwork.remoteUsers.TryGetValue(_value, out var user) ? user.visualName : "User#" + _value;
        }
        public static implicit operator ulong(UserID id) => id._value;
        public static implicit operator UserID(ulong id) => new UserID(id);
        public override int GetHashCode() => _value.GetHashCode();
    }
}