using System;

namespace SolusManifestApp.Tools.DepotDumper
{
    public class DumperConfig
    {
        public bool RememberPassword { get; set; }
        public bool DumpUnreleased { get; set; }
        public uint TargetAppId { get; set; }
        public bool UseQrCode { get; set; }
    }
}
