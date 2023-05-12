using System;
using System.IO.Ports;

namespace Fingerprinter;

public class SafeSerialPort : SerialPort {
    public SafeSerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        : base(portName, baudRate, parity, dataBits, stopBits) { }
    public SafeSerialPort(string portName, int baudRate)
        : base(portName, baudRate) { }
    public SafeSerialPort()
        : base() { }

    public new bool IsOpen {
        get {
            bool open;
            try {
                open = base.CtsHolding;
                open = base.IsOpen;
            } catch {
                open = false;
            }
            return open;
        }
    }
    public bool WasOpened  => base.IsOpen;

    public new bool Open() {
        if (!IsOpen) {
            try {
                if (WasOpened) Close();
                base.PortName = Program.Port;
                base.BaudRate = Program.BaudRate;
                base.Parity = Parity.None;
                base.DataBits = 8;
                base.StopBits = StopBits.One;
                base.ErrorReceived += OnError;
                base.Open();
            } catch (Exception e) {
                Logger.Exception("Error opening serial port", e);
            }
        }
        return IsOpen;
    }

    private void OnError(object sender, SerialErrorReceivedEventArgs e) {
        Logger.Error($"Serial port error: {e.EventType}");
    }

    public new void Write(byte[] buffer, int offset, int count)
    {
        if (Open()) {
            try
            {
                base.Write(buffer, offset, count);
            }
            catch (Exception e)
            {
                Logger.Exception("Failed to write to serial port", e);
            }
        } else {
            Logger.Error("Failed to write to serial port");
        }
    }
    public new byte? ReadByte() {
        if (Open()) {
            try {
                return (byte)base.ReadByte();
            } catch (Exception e) {
                Logger.Exception("Failed to read from serial port", e);
            }
        } else {
            Logger.Error("Failed to read from serial port");
        }
        return null;
    }
}
