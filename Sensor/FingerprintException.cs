using System;

namespace Fingerprinter;

public class FingerprintException : Exception {
    public FingerprintException(ErrorCode error, string message = "") : base($"{error}: {message}") { }
}
