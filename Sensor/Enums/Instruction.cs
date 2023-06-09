namespace Fingerprinter;

public enum Instruction : byte {
    GenImg          = 0x01,
    Img2Tz          = 0x02,
    Match           = 0x03,
    Search          = 0x04,
    RegModel        = 0x05,
    Store           = 0x06,
    LoadChar        = 0x07,
    UpChar          = 0x08,
    DownChr         = 0x09,
    UpImage         = 0x0a,
    DownImage       = 0x0b,
    DeletChar       = 0x0c,
    Empty           = 0x0d,
    SetSysPara      = 0x0e,
    ReadSysPara     = 0x0f,
    SetPwd          = 0x12,
    VfyPwd          = 0x13,
    GetRandomCode   = 0x14,
    SetAddr         = 0x15,
    ReadInfoPage    = 0x16,
    WriteNotepad    = 0x18,
    ReadNotepad     = 0x19,
    HighspeedSearch = 0x1b,
    TemplateNum     = 0x1d,
    ReadConList     = 0x1f,
    AuraLedConfig   = 0x35,
    GetAlgVer       = 0x39,
    GetFwVer        = 0x3A,
    ReadProdInfo    = 0x3C,
    OpenLED         = 0x50,
    CloseLED        = 0x51,
    GetImageFree    = 0x52,
    GetEcho         = 0x53,
    AutoLogin       = 0x54,
    AutoSearch      = 0x55,
    SearchResBack   = 0x56
}
