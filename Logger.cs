using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Fingerprinter;

[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoggerTarget {
    None   = 0,
    StdOut = 1 << 0,
    Trace  = 1 << 1,
    Debug  = 1 << 2,
    File   = 1 << 3,
    Default = StdOut | File,
    All    = StdOut | Trace | Debug | File
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogLevel {
    All = 0,
    Trace = 10,
    Debug = 20,
    Info = 30,
    Warning = 40,
    Error = 50,
    Exception = 60,
    None = 70
}

[Flags]
public enum ANSIFormatting
{
    None = 0,
    Bold = 1 << 0,
    Faint = 1 << 1,
    Italic = 1 << 2,
    Underlined = 1 << 3,
    Overlined = 1 << 4,
    Blink = 1 << 5,
    Inverted = 1 << 6,
    StrikeThrough = 1 << 7,
    LowerCase = 1 << 8,
    UpperCase = 1 << 9,
    Clear = 1 << 10,
}

public static class Logger {
    private const string LogFileName = "fingerprint.log";
    public const string EscapeSequence = "\u001b";
    public const string ControlHeader = EscapeSequence + "[";
    public static string ControlCode(params byte[] codes) => $"{ControlHeader}{string.Join(";", codes.Select(c => c.ToString()))}m";

    public static string Reset = ControlCode(0);
    public static string Bold = ControlCode(1);
    public static string Faint = ControlCode(2);
    public static string Italic = ControlCode(3);
    public static string Underlined = ControlCode(4);
    public static string Blink = ControlCode(5);
    public static string Inverted = ControlCode(7);
    public static string StrikeThrough = ControlCode(9);
    public static string Overlined = ControlCode(53);
    
    public static string Foreground(Color color) => ControlCode(38, 2, color.R, color.G, color.B);
    public static string Background(Color color) => ControlCode(48, 2, color.R, color.G, color.B);


    private static readonly Color TraceColor = Color.Lime;
    private static readonly Color DebugColor = Color.LightGreen;
    private static readonly Color InfoColor = Color.White;
    private static readonly Color WarningColor = Color.Yellow;
    private static readonly Color ErrorColor = Color.Red;
    private static readonly Color ExceptionColor = Color.Red;
    private static readonly ANSIFormatting TraceFormat = ANSIFormatting.None;
    private static readonly ANSIFormatting DebugFormat = ANSIFormatting.None;
    private static readonly ANSIFormatting InfoFormat = ANSIFormatting.None;
    private static readonly ANSIFormatting WarningFormat = ANSIFormatting.None;
    private static readonly ANSIFormatting ErrorFormat = ANSIFormatting.None;
    private static readonly ANSIFormatting ExceptionFormat = ANSIFormatting.Bold;

    private static LogLevel _logLevel = LogLevel.None;
    private static LoggerTarget _logTarget = LoggerTarget.Default;
    private static bool _logColorizer = true;

    public static void Format(string message = "", Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) {
        if(level < _logLevel) return;
        var target = _logTarget;
        if(target.HasFlag(LoggerTarget.File))
            try {
                File.AppendAllText(LogFileName, message);
            } catch {}
        if (_logColorizer) {
            var formatted = false;
            if(foreground.HasValue) {
                message = Foreground(foreground.Value) + message;
                formatted = true;
            }
            if(background.HasValue) {
                message = Background(background.Value) + message;
                formatted = true;
            }
            if(formatting.HasValue) {
                if(formatting.Value.HasFlag(ANSIFormatting.Bold)) {
                    message = Bold + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Faint)) {
                    message = Faint + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Italic)) {
                    message = Italic + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Underlined)) {
                    message = Underlined + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Overlined)) {
                    message = Overlined + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Blink)) {
                    message = Blink + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.Inverted)) {
                    message = Inverted + message;
                    formatted = true;
                }
                if(formatting.Value.HasFlag(ANSIFormatting.StrikeThrough)) {
                    message = StrikeThrough + message;
                    formatted = true;
                }
            }
            if (formatted)
                message += Reset;
        }
        if(target.HasFlag(LoggerTarget.StdOut))
            System.Console.Write(message);
        if(target.HasFlag(LoggerTarget.Trace))
            System.Diagnostics.Trace.Write(message);
        if(target.HasFlag(LoggerTarget.Debug))
            System.Diagnostics.Debug.Write(message);
    }

    public static void Write(string message = "", Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) => Format(message, foreground, background, formatting, level);
    public static void WriteLine(string message = "", Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) => Write(message + Environment.NewLine, foreground, background, formatting, level);
    public static void WriteException(Exception ex, Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) => WriteLine(ex.Message, foreground??Color.Red, background, formatting, level);
    public static void Timestamp(Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) => Write($"[{DateTime.Now:yyyyMMddTHHmmssfff}] ", foreground, background, formatting, level);
    public static void Level(Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) => Write($"{level} ".ToUpper(), foreground, background, formatting, level);
    public static void Location(string? callerFunc, string? callerFile, int? callerLine, Color? foreground = null, Color? background = null, ANSIFormatting? formatting = null, LogLevel level = LogLevel.Info) {
        // Write($"{callerFunc} @ {System.IO.Path.GetRelativePath(Environment.CurrentDirectory, callerFile ?? string.Empty)}:{callerLine} ", foreground, background, formatting, level);
        Write($"{callerFunc} @ {callerFile}:{callerLine} ", foreground, background, formatting, level);
    }

    private static void Log(string message, Color foreground, Color background, LogLevel level, ANSIFormatting formatting = ANSIFormatting.None, [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Timestamp(foreground, background, formatting | ANSIFormatting.Faint, level);
        Level(foreground, background, formatting | ANSIFormatting.Faint, level);
        Location(callerFunc, callerFile, callerLine, foreground, background, formatting, level);
        WriteLine(message, foreground, background, formatting, level);
    }
    public static void Trace(string message = "", [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message, TraceColor, Color.Black, LogLevel.Trace, TraceFormat, callerFunc, callerFile, callerLine);
    }
    public static void Debug(string message = "", [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message, DebugColor, Color.Black, LogLevel.Debug, DebugFormat, callerFunc, callerFile, callerLine);
    }
    public static void Info(string message = "", [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message, InfoColor, Color.Black, LogLevel.Info, InfoFormat, callerFunc, callerFile, callerLine);
    }
    public static void Warning(string message = "", [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message, WarningColor, Color.Black, LogLevel.Warning, WarningFormat, callerFunc, callerFile, callerLine);
    }
    public static void Error(string message = "", [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message, ErrorColor, Color.Black, LogLevel.Error, ErrorFormat, callerFunc, callerFile, callerLine);
    }
    public static void Exception(string message = "", Exception? ex = null, [CallerMemberName] string? callerFunc = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int? callerLine = null) {
        Log(message + Environment.NewLine + ex?.Message + Environment.NewLine + ex?.StackTrace, ExceptionColor, Color.Black, LogLevel.Exception, ExceptionFormat, callerFunc, callerFile, callerLine);
    }
}