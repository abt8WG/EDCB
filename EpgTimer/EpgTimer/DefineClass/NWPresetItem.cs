using System;
using System.Security;

namespace EpgTimer
{
    public class NWPresetItem
    {
        public string Name { get; set; }
        public string NWServerIP { get; set; }
        public UInt32 NWServerPort { get; set; }
        public UInt32 NWWaitPort { get; set; }
        public string NWMacAdd { get; set; }
        public SerializableSecureString NWPassword { get; set; }
        public override string ToString() { return Name; }

        public NWPresetItem()
        {
            NWPassword = new SerializableSecureString();
        }
        public NWPresetItem(string name, string ip, UInt32 sport, UInt32 wport, string mac, SerializableSecureString password)
        {
            Name = name;
            NWServerIP = ip;
            NWServerPort = sport;
            NWWaitPort = wport;
            NWMacAdd = mac;
            NWPassword = password;
        }
        public bool EqualsTo(NWPresetItem item, bool ignoreName = false)
        {
            return (ignoreName == true || Name == item.Name)
                && NWServerIP == item.NWServerIP && NWServerPort == item.NWServerPort
                && NWWaitPort == item.NWWaitPort && NWMacAdd == item.NWMacAdd;
            /* パスワードは比べなくてもいいかな？ */
        }
        public NWPresetItem Clone() { return CopyObj.Clone(this, CopyData); }
        protected static void CopyData(NWPresetItem src, NWPresetItem dest)
        {
            dest.Name = src.Name;
            dest.NWServerIP = src.NWServerIP;
            dest.NWServerPort = src.NWServerPort;
            dest.NWWaitPort = src.NWWaitPort;
            dest.NWMacAdd = src.NWMacAdd;
            dest.NWPassword = src.NWPassword;
        }
    }
}
