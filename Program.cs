namespace Fingerprinter;

public class Program {
    public static string Port = SafeSerialPort.GetPortNames().LastOrDefault() ?? throw new Exception("No serial ports found.");
    public static int BaudRate = 57600;

    public static void Main(string  [] args) {
        Run().Wait();
    }
    public static async Task Run() {
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
            Logger.Write("Bye!");
        } catch (Exception e) {
            Logger.Write("Unexpected error occured: " + e.ToString());
        }
    }
}