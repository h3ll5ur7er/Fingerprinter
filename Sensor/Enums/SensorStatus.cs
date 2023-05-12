namespace Fingerprinter;

public class SensorStatus {
    public ushort StatusRegister { get; set; }
    public ushort SystemId { get; set; }
    public ushort Capacity { get; set; }
    public ushort SecurityLevel { get; set; }
    public uint DeviceAddress { get; set; }
    public ushort PacketLength { get; set; }
    public ushort BaudRate { get; set; }
    public string? ProductType { get; set; }
    public string? Version { get; set; }
    public string? Manufacturer { get; set; }
    public string? SensorId { get; set; }
}
