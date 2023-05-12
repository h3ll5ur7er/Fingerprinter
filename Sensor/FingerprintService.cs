using System;
using System.Threading.Tasks;

namespace Fingerprinter;

public delegate void StatusUpdateHandler (string message);

public class FingerprintService {
    private static FingerprintService? _instance;
    public static FingerprintService Instance => _instance ??= new FingerprintService();

    public FingerprintSensor Sensor { get; private set; }

    public event StatusUpdateHandler? StatusUpdate;
    private FingerprintService() {
        Sensor = new FingerprintSensor();
    }

    public async Task Connect() {
        if (Sensor.IsOpen) {
            Sensor.close();
        }
        await Sensor.begin();
    }

    public async Task ClearFingerprintDatabase() {
        if(await Sensor.verifyPassword()) {
            await Sensor.emptyDatabase();
        }
    }

    public async Task Enroll(ushort? id = null, Action<string>? statusUpdate = null) {
        if (statusUpdate == null) statusUpdate = msg=>StatusUpdate?.Invoke(msg);
        statusUpdate += msg => Logger.Info(msg);
        await Sensor.enrollFingerprint(statusUpdate, id);
    }
    public async Task Delete(ushort id) {
        if(await Sensor.verifyPassword()) {
            await Sensor.deleteModel(id);
        }
    }
    public async Task<ushort?> Search() {
        if(!await Sensor.verifyPassword()) return null;
        try {
            await Sensor.readFinger(msg=>{});
            await Sensor.generateTemplate(1);
            var (error, result) = await Sensor.fingerSearch(1);
            if (error == ErrorCode.Ok) {
                Logger.Info($"Found fingerprint at ID #{result.FingerId} with confidence {result.Confidence}.");
                return result.FingerId;
            }
            return null;
        } catch (FingerprintException e) {
            Logger.Trace("Failed to find finger in database: " + e.Message);
            return null;
        } catch (Exception e) {
            Logger.Exception("Unexpected error occured while finding finger in database: ", e);
            return null;
        }
    }
    public async Task Download() {
        // await Sensor.transferImageFromSensor("image.png");
        var (err, info) = await Sensor.readInformationPage();
        if (err != ErrorCode.Ok) {
            Logger.Error($"Failed to read info page: {err}");
            return;
        }
        Logger.Info($"Read info page: {err}");
        (err, var data) = await Sensor.readProductInfo();
        Logger.Info($"Read product info: {err}");
            (err, data) = await Sensor.getAlgoVersion();
        Logger.Info($"Read algo version: {err}");
            (err, data) = await Sensor.getFwVersion();
        Logger.Info($"Read fw version: {err}");
    }
    public async Task<(ErrorCode, byte[])> Download(int templateIndex) {
        return await Sensor.transferModelFromSensor(templateIndex);
    }
}
