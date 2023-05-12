using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;

namespace Fingerprinter;

public delegate void FingerprintSensorTouchHandler();

public class FingerprintSensor
{
    internal const ushort FINGERPRINT_STARTCODE = 0xEF01;
    const ushort DEFAULTTIMEOUT = 1000;
    const ushort DEBOUNCE_TIME_S = 2;

    private SafeSerialPort serial;
    private DateTime debounce = DateTime.Now;
    private bool enrolling = false;
    private bool reading = false;
    public bool IsOpen => serial.IsOpen;

    public event FingerprintSensorTouchHandler? OnTouch;

    public FingerprintSensor() {
        serial = new SafeSerialPort();
        serial.PinChanged += OnPinChanged;
    }

    private void OnPinChanged(object sender, SerialPinChangedEventArgs e)
    {
        if (enrolling) return; // Don't trigger touch events while enrolling
        if (reading) return; // Don't trigger touch events while logging in
        Logger.Trace($"Serial pin event: {e.EventType}");
        if(e.EventType == SerialPinChange.DsrChanged) {
            var timeSinceLastAction = DateTime.Now - debounce;
            if(timeSinceLastAction > TimeSpan.FromSeconds(2)) {
                OnTouch?.Invoke();
                debounce = DateTime.Now;
            } else {
                Logger.Trace($"Ignoring touch event, debounce time not elapsed: {timeSinceLastAction.TotalSeconds}");
            }
        } else {
            Logger.Trace($"Ignoring touch event, unknown event type: {e.EventType}");
        }
    }

    public async Task begin() {
        await Task.Delay(100);
        try {
            serial.Open();
        } catch (Exception e) {
            Logger.Exception("Failed to open serial port", e);
        }
    }
    public void close() => serial.Close();


    // Fingerprint functions
    public async Task<ErrorCode> getImage() => await sendCmdPacket(Instruction.GenImg);
    public async Task<ErrorCode> image2Tz(byte slot = 1) => await sendCmdPacket(Instruction.Img2Tz, slot);
    public async Task<ErrorCode> createModel() => await sendCmdPacket(Instruction.RegModel);
    public async Task<ErrorCode> emptyDatabase() => await sendCmdPacket(Instruction.Empty);
    public async Task<(ErrorCode, byte[]?)> readIndexTable(byte page) {
        var (error, packet) = await getCmdPacket(Instruction.ReadConList, page);
        if(error != ErrorCode.Ok) return (error, null);
        if(packet.data[0] != 0x00) return ((ErrorCode)packet.data[0], null);
        return (ErrorCode.Ok, packet.data.Skip(1).Take(packet.length-2).ToArray());
    }
    public async Task<ErrorCode> storeModel(ushort location) => await sendCmdPacket(Instruction.Store, 0x01, (byte)(location >> 8), (byte)(location & 0xFF));
    public async Task<ErrorCode> loadModel(ushort location) => await sendCmdPacket(Instruction.LoadChar, 0x01, (byte)(location >> 8), (byte)(location & 0xFF));
    public async Task<ErrorCode> readInfoPage() => await sendCmdPacket(Instruction.ReadInfoPage);
    public async Task<(ErrorCode, Message)> getAlgoVersion() => await getCmdPacket(Instruction.GetAlgVer);
    public async Task<(ErrorCode, Message)> getFwVersion() => await getCmdPacket(Instruction.GetFwVer);
    public async Task<(ErrorCode, Message)> readProductInfo() => await getCmdPacket(Instruction.ReadProdInfo);
    public async Task<ErrorCode> getModel() => await sendCmdPacket(Instruction.UpChar, 0x01);
    public async Task<ErrorCode> deleteModel(ushort location) => await sendCmdPacket(Instruction.DeletChar, (byte)(location >> 8), (byte)(location & 0xFF), 0x00, 0x01);
    public async Task<ErrorCode> transferImageFromSensor(string filePath) {
        ErrorCode err = ErrorCode.Timeout;
        if((err = await checkPassword()) != ErrorCode.Ok) return err;
        await readFinger(m=>{});
        err = await sendCmdPacket(Instruction.UpImage);
        List<Message> messages = new List<Message>();
        Message msg;
        while (err == ErrorCode.Ok){
            (err, msg) = await SerialMessageParser.ReceivePacket(serial);
            if(err != ErrorCode.Ok) break;
            messages.Add(msg);
            if(msg.type == PacketType.EndData) break;
        }
        List<byte> data = new List<byte>();
        foreach (var message in messages) {
            data.AddRange(message.data.Take(message.length-2));
        }
        var dataArray = data.ToArray();
        var imageSizeLookup = new Dictionary<int, (int w, int h)>{
            {(160*160)/2, (160, 160)},
            {(192*192)/2, (192, 192)},
            {(256*288)/2, (256, 288)}
        };
        var (width, height) = imageSizeLookup[dataArray.Length];
        var halfWidth = width/2;
        var bmp = new SKBitmap(width, height);
        if(dataArray.Length != (height * halfWidth)){
            Logger.Error($"Invalid image data length: {dataArray.Length}");
            return ErrorCode.BadPacket;
        }
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < halfWidth; x++) {
                var byteIndex = y * halfWidth + x;
                var dualPixelValue = dataArray[byteIndex];
                var pixel0 = (dualPixelValue & 0xF0) >> 4;
                var pixel1 = dualPixelValue & 0x0F;
                bmp.SetPixel(x * 2, y, new SKColor((byte)(pixel0 * 16), (byte)(pixel0 * 16), (byte)(pixel0 * 16)));
                bmp.SetPixel(x * 2 + 1, y, new SKColor((byte)(pixel1 * 16), (byte)(pixel1 * 16), (byte)(pixel1 * 16)));
            }
        }
        using (var image = SKImage.FromBitmap(bmp))
        using (var data2 = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = System.IO.File.OpenWrite(filePath))
        data2.SaveTo(stream);

        return ErrorCode.Ok;
    }

    public async Task<(ErrorCode, List<int>)> ReadIdOccupancyList() {
        
        ErrorCode err = ErrorCode.Timeout;
        if((err = await checkPassword()) != ErrorCode.Ok) return (err, new List<int>());
        var databaseOccupancy = new List<byte[]>();
        for (byte i = 0; i < 4; i++) {
            (err, var table) = await readIndexTable(i);
            if (err != ErrorCode.Ok) {
                break;
            }
            databaseOccupancy.Add(table?? throw new Exception("Table is null"));
        }
        var occupiedIds = databaseOccupancy.SelectMany(table => table.SelectMany(b => Convert.ToString(b, 2).PadLeft(8, '0').Reverse())).Select((c,i) => new{c,i}).Where(x => x.c == '1').Select(x=> x.i).ToList();
        Logger.Trace($"Occupied ids: {string.Join(", ", occupiedIds)}");
        return (ErrorCode.Ok, occupiedIds);
    }
    public async Task<(ErrorCode, byte[])> transferModelFromSensor(int templateIndex) {
        ErrorCode err = ErrorCode.Timeout;
        if((err = await checkPassword()) != ErrorCode.Ok) return (err, new byte[]{});
        err = await loadModel((ushort)templateIndex);
        if (err != ErrorCode.Ok) {
            Logger.Error($"Failed to load model {templateIndex} from sensor: {err}");
            return (err, new byte[]{});
        }
        err = await sendCmdPacket(Instruction.UpChar, 0x01);
        List<Message> messages = new List<Message>();
        Message msg;
        while (err == ErrorCode.Ok){
            (err, msg) = await SerialMessageParser.ReceivePacket(serial);
            if(err != ErrorCode.Ok) break;
            messages.Add(msg);
            if(msg.type == PacketType.EndData) break;
        }
        List<byte> data = new List<byte>();
        foreach (var message in messages) {
            data.AddRange(message.data.Take(message.length-2));
        }
        var dataArray = data.ToArray();
        return (ErrorCode.Ok, dataArray);
    }
    public async Task<(ErrorCode, SensorStatus?)> readInformationPage() {
        ErrorCode err = ErrorCode.Timeout;
        if((err = await checkPassword()) != ErrorCode.Ok) return (err, null);
        err = await readInfoPage();
        if (err != ErrorCode.Ok) {
            Logger.Error($"Failed to read information page: {err}");
            return (err, null);
        }
        List<Message> messages = new List<Message>();
        Message msg;
        while (err == ErrorCode.Ok){
            (err, msg) = await SerialMessageParser.ReceivePacket(serial);
            if(err != ErrorCode.Ok) break;
            messages.Add(msg);
            if(msg.type == PacketType.EndData) break;
        }
        List<byte> data = new List<byte>();
        foreach (var message in messages) {
            data.AddRange(message.data.Take(message.length-2));
        }
        
        var dataArray = data.ToArray();
        var parts = dataArray.Partition(2, 2, 2, 2, 4, 2, 2, 12, 8, 8, 8, 8, 66, 2);
        var info = new SensorStatus{
            StatusRegister = parts[ 0].ReadUShort(),
            SystemId       = parts[ 1].ReadUShort(),
            Capacity       = parts[ 2].ReadUShort(),
            SecurityLevel  = parts[ 3].ReadUShort(),
            DeviceAddress  = parts[ 4].ReadUInt(),
            PacketLength   = parts[ 5].ReadUShort(),
            BaudRate       = parts[ 6].ReadUShort(),
            // Something      = parts[ 7], // 12 bytes, meaning unknown
            ProductType    = parts[ 8].ReadString().Trim(),
            Version        = parts[ 9].ReadString().Trim(),
            Manufacturer   = parts[10].ReadString().Trim(),
            SensorId       = parts[11].ReadString().Trim(),
            // Padding        = parts[12], // Zero padding
            // Number         = parts[13].ReadUShort(), // Some number that is always 0x1234
        };

        return (ErrorCode.Ok, info);
    }

    public async Task<(ErrorCode, SearchResult)> fingerFastSearch(byte slot = 1, ushort startPage = 0, ushort pageCount = 0xA3) {
        // high speed search of slot #1 starting at page 0x0000 and page #0x00A3
        var (error, packet) = await getCmdPacket(Instruction.HighspeedSearch, slot, (byte)(startPage >> 8), (byte)(startPage & 0xFF), (byte)(pageCount >> 8), (byte)(pageCount & 0xFF));

        var parts = packet.data.Partition(1, 2, 2);

        var result = new SearchResult { 
            FingerId = parts[1].ReadUShort(),
            Confidence = parts[2].ReadUShort()
        };

        return ((ErrorCode)packet.data[0], result);
    }
    public async Task<(ErrorCode, SearchResult)> fingerSearch(byte slot = 1, ushort start = 0, ushort count = 0xFFFF) {
        // search of slot starting thru the capacity
        var (error, packet) = await getCmdPacket(Instruction.Search, slot, (byte)(start >> 8), (byte)(start & 0xFF), (byte)(count >> 8), (byte)(count & 0xFF));

        var parts = packet.data.Partition(1, 2, 2);

        var result = new SearchResult { 
            FingerId = parts[1].ReadUShort(),
            Confidence = parts[2].ReadUShort()
        };

        return ((ErrorCode)packet.data[0], result);
    }
    public async Task<(ErrorCode, ushort)> getTemplateCount() {
        var (error, packet) = await getCmdPacket(Instruction.TemplateNum);
        var parts = packet.data.Partition(1, 2, 2);
        var templateCount = parts[1].ReadUShort();

        return ((ErrorCode)packet.data[0], templateCount);
    }
    internal async Task readFinger(Action<string> statusUpdate) {
        if(reading) return;
        var error = ErrorCode.NoFinger;
        var tic = DateTime.Now;
        while(error != ErrorCode.Ok) {
            var toc = DateTime.Now;
            if((toc - tic).TotalSeconds > 10) {
                statusUpdate("Timeout");
                reading = false;
                throw new FingerprintException(ErrorCode.Timeout, "Timeout while reading fingerprint");
            }
            reading = true;
            error = await getImage();
            switch (error) {
                case ErrorCode.Ok:
                    statusUpdate("Image taken");
                    reading = false;
                    return;
                case ErrorCode.NoFinger:
                    statusUpdate("Waiting for finger");
                    break;
                case ErrorCode.PacketReceiveError:
                    statusUpdate("Communication error");
                    break;
                case ErrorCode.ImageFail:
                    statusUpdate("Sensor error");
                    break;
                default:
                    statusUpdate("Unknown error");
                    break;
            }
            await Task.Delay(100);
        }
        reading = false;
    }
    internal async Task generateTemplate(byte slot) {
        Logger.Debug($"Converting image in slot {slot} to template");
        var error = await image2Tz(slot);
        switch (error) {
            case ErrorCode.Ok:
                Logger.Debug("Image converted");
                return;
            case ErrorCode.ImageMess:
                throw new FingerprintException(error, "Image too messy");
            case ErrorCode.PacketReceiveError:
                throw new FingerprintException(error, "Communication error");
            case ErrorCode.FeatureFail:
                throw new FingerprintException(error, "Could not find fingerprint features");
            case ErrorCode.InvalidImage:
                throw new FingerprintException(error, "Could not find fingerprint features");
            default:
                throw new FingerprintException(error, "Unknown error");
        }
    }
    private async Task createAndStoreModel(ushort id) {
        var error = await createModel();
        switch (error) {
            case ErrorCode.Ok:
                Logger.Debug("Templates combined");
                break;
            case ErrorCode.PacketReceiveError:
                throw new FingerprintException(error, "Communication error");
            case ErrorCode.EnrollMismatch:
                throw new FingerprintException(error, "Fingerprints did not match");
            default:
                throw new FingerprintException(error, "Unknown error");
        }
        error = await storeModel(id);
        switch (error) {
            case ErrorCode.Ok:
                Logger.Debug("Stored!");
                break;
            case ErrorCode.PacketReceiveError:
                throw new FingerprintException(error, "Communication error");
            case ErrorCode.BadLocation:
                throw new FingerprintException(error, "Could not store in that location");
            case ErrorCode.FlashErr:
                throw new FingerprintException(error, "Error writing to flash");
            default:
                throw new FingerprintException(error, "Unknown error");
        }
    }
    public async Task<int> enrollFingerprint(Action<string> statusUpdate, int? id = null) {
        if (enrolling) return -1;
        if (!IsOpen) return -1;
        if(!await verifyPassword()) return -1;

        enrolling = true;
        try {
            var (err, count) = await getTemplateCount();

            await readFinger(statusUpdate);
            await generateTemplate(1);
            await transferImageFromSensor($"fingerprints/fingerprint_{id:D3}-1.png");

            statusUpdate("Lift finger and place it again");
            await Task.Delay(2000);

            await readFinger(statusUpdate);
            await generateTemplate(2);
            await transferImageFromSensor($"fingerprints/fingerprint_{id:D3}-2.png");

            await createAndStoreModel((ushort)(id.HasValue ? id.Value : count));
            (err, var modelData) = await transferModelFromSensor((ushort)(id.HasValue ? id.Value : count));
            System.IO.File.WriteAllBytes($"fingerprints/fingerprint_{id:D3}.bin", modelData);
            enrolling = false;
            return count;
        } catch(FingerprintException e) {
            statusUpdate(e.Message);
            enrolling = false;
            return -1;
        }
    }

    // Password functions
    private async Task<ErrorCode> checkPassword() {
        var thePassword = 0x00000000;
        var (error, msg) = await getCmdPacket(Instruction.VfyPwd, (byte)(thePassword >> 24),
                        (byte)(thePassword >> 16), (byte)(thePassword >> 8),
                        (byte)(thePassword & 0xFF));
        if (error != ErrorCode.Ok) return error;
        return (ErrorCode)msg.data[0];
    }
    public async Task<ErrorCode> setPassword(uint password) => await sendCmdPacket(Instruction.SetPwd, (byte)(password >> 24), (byte)(password >> 16), (byte)(password >> 8), (byte)(password & 0xFF));
    public async Task<bool> verifyPassword() => await checkPassword() == ErrorCode.Ok;

    // LED functions
    public async Task<ErrorCode> LEDcontrol(bool on) => await sendCmdPacket(on ? Instruction.OpenLED : Instruction.CloseLED);
    public async Task<ErrorCode> LEDcontrol(LedSpeed control, byte speed, LedColor coloridx, byte count = 0) => await sendCmdPacket(Instruction.AuraLedConfig, (byte)control, speed, (byte)coloridx, count);

    // Sensor functions
    public async Task<(ErrorCode, SensorStatus?)> getParameters() {

        var (error, packet) = await getCmdPacket(Instruction.ReadSysPara);
        if(error != ErrorCode.Ok) return (error, null);

        var parts = packet.data.Partition(1, 2, 2, 2, 2, 4, 2, 2);
        var status = new SensorStatus{
            StatusRegister = parts[1].ReadUShort(),
            SystemId = parts[2].ReadUShort(),
            Capacity = parts[3].ReadUShort(),
            SecurityLevel = parts[4].ReadUShort(),
            DeviceAddress = parts[5].ReadUInt(),
            PacketLength = (ushort)(1 << (5 + parts[6].ReadUShort())),
            BaudRate = (ushort)(parts[7].ReadUShort() * 9600)
        };

        return ((ErrorCode)packet.data[0], status);
    }
    public async Task<ErrorCode> setBaudRate(BaudRate baudrate) => await writeRegister(Registers.BaudRate, (byte)baudrate);
    public async Task<ErrorCode> setSecurityLevel(SecurityLevel level) => await writeRegister(Registers.SecurityLevel, (byte)level);
    public async Task<ErrorCode> setPacketSize(PacketSize size) => await writeRegister(Registers.PacketSize, (byte)size);

    // Communication functions

    private async Task<ErrorCode> writeRegister(Registers regAdd, byte value) => await sendCmdPacket(Instruction.SetSysPara, (byte)regAdd, value);

    private async Task<(ErrorCode error, Message packet)> getCmdPacket(Instruction type, params byte[] data) {
        Logger.Trace($"Sending packet:  {type}({data.Hex()})");
        var request = Message.MakeCommand(type, data);
        SerialMessageParser.SendPacket(request, serial);
        try {
            var (error, response) = await SerialMessageParser.ReceivePacket(serial);
            if (error != ErrorCode.Ok) {
                return (ErrorCode.PacketReceiveError, response);
            }
            if (response.type != PacketType.Ack) {
                return (ErrorCode.PacketReceiveError, response);
            }
            return (ErrorCode.Ok, response);
        } catch {
            throw new FingerprintException(ErrorCode.PacketReceiveError, "Error receiving packet");
        }
    }

    private async Task<ErrorCode> sendCmdPacket(Instruction type, params byte[] data) {
        var (error, msg) = await getCmdPacket(type, data);
        var returnCode  = (ErrorCode)msg.data[0];
        Logger.Trace($"Packet response: {type}({returnCode})");
        return returnCode;
    }
}
