namespace Fingerprinter;

public enum Registers : byte {
    PacketSize = 0x06, //!< Packet size register address
    SecurityLevel = 0x05, //!< Security level register address: The safety level is 1 The highest rate of false recognition , The rejection rate is the lowest . The safety level is 5 The lowest tate of false recognition, The rejection rate is the highest .
    BaudRate = 0x04,   //!< BAUDRATE register address

}
