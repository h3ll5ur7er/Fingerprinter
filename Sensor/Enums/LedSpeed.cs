namespace Fingerprinter;

public enum LedSpeed : byte {
    
    Breathing = 0x01,   //!< Breathing light
    Flashing = 0x02,    //!< Flashing light
    On = 0x03,          //!< Always on
    Off = 0x04,         //!< Always off
    GradualOn = 0x05,   //!< Gradually on
    GradualOff = 0x06,  //!< Gradually off
}
