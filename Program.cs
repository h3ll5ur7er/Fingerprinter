using System;
using System.Linq;
using System.Threading.Tasks;

namespace Fingerprinter;

public class Program {
    public static string[] Ports = SafeSerialPort.GetPortNames();
    public static string? Port;
    public static int BaudRate = 57600;

    public static void Main(string  [] args) {
        Run().Wait();
    }
    public static async Task Run() {
        if (Ports.Length == 0) {
            Logger.Write("No Serial ports found!", level: LogLevel.None);
            return;
        } else if (Ports.Length == 1) {
            Port = Ports[0];
        } else {
            Logger.Write($"Please select the correct Serial port:", level: LogLevel.None);
            for (int i = 0; i < Ports.Length; i++)
            {
                Logger.Write($"[{i+1}]  {Ports[i]}", level: LogLevel.None);
            }
            while(Port == null){
                var input = Console.ReadLine();
                if (int.TryParse(input, out var portIndex) && portIndex > 0 && portIndex <= Ports.Length) {
                    Port = Ports[portIndex-1];
                } else {
                    Logger.Write("Invalid input! Enter the number of the port you want to use.", level: LogLevel.None);
                }
            }
        }
        Logger.Write("Using port: " + Port, level: LogLevel.None);



        try {
            await FingerprintService.Instance.Connect();
            var counter = 0;
            while(FingerprintService.Instance.Sensor.IsOpen) {
                var error = await FingerprintService.Instance.Sensor.getImage();
                Logger.Write($"\r{DateTime.Now:yyyyMMddTHHmmssfff}  Place finger on sensor   (status: {error})                                   ", level: LogLevel.None);
                if (error == ErrorCode.Ok) {
                    Logger.Write($"\r{DateTime.Now:yyyyMMddTHHmmssfff}  Found Finger #{counter}                                                  ", level: LogLevel.None);
                    Logger.Write($"\n{DateTime.Now:yyyyMMddTHHmmssfff}  Dowloading image from sensor, please wait...                                   ", level: LogLevel.None);

                    var getImgErr = await FingerprintService.Instance.Sensor.transferImageFromSensor($"{DateTime.Now:yyyyMMddTHHmmssfff}_{counter++:D3}.png");
                    if(getImgErr != ErrorCode.Ok)
                        Logger.Write($"\n{DateTime.Now:yyyyMMddTHHmmssfff}  Error occured while downloading image from sensor: {getImgErr}                                  ", level: LogLevel.None);
                }
                await Task.Delay(100);
            }
            Logger.Write("Bye!", level: LogLevel.None);
        } catch (Exception e) {
            Logger.Write("Unexpected error occured: " + e.ToString(), level: LogLevel.None);
        }
    }
}