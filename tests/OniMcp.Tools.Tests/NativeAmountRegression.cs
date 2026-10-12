using System;
using System.Collections.Generic;
using OniMcp.Tools;

internal static class NativeAmountRegression
{
    internal static void Run()
    {
        var zero = NativeAmountSnapshot.Read(0, () => 0, () => 200, () => -1);
        Check((double)zero["value"] == 0 && (bool)zero["available"], "valid zero is observed, not missing");
        Check((double?)zero["rangeFraction"] == 0, "zero fraction preserves empty value");
        var correctedCapacity = NativeAmountSnapshot.Read(150, () => 0, () => 300, () => 0);
        Check((double)correctedCapacity["maximum"] == 300 && (double?)correctedCapacity["rangeFraction"] == .5, "individual modified maximum, not base default");
        var failed = NativeAmountSnapshot.Read(150, () => 0, () => { throw new InvalidOperationException(); }, () => -2);
        Check(failed["maximum"] == null && failed["rangeFraction"] == null && !(bool)failed["available"], "failed max does not invent a normal default");
        Check((double)failed["value"] == 150 && (double)failed["attributeDeltaPerSecond"] == -2, "other successful values survive failed max");
        var invalid = NativeAmountSnapshot.Read(float.NaN, () => 0, () => float.PositiveInfinity, () => float.NegativeInfinity);
        Check(invalid["value"] == null && invalid["maximum"] == null && invalid["attributeDeltaPerSecond"] == null && ((List<string>)invalid["errors"]).Count == 3, "nonfinite values remain explicit missing");
        var emptyRange = NativeAmountSnapshot.Read(0, () => 0, () => 0, () => 0);
        Check((bool)emptyRange["available"] && emptyRange["rangeFraction"] == null, "observed zero capacity is not a percentage");
        var shiftedRange = NativeAmountSnapshot.Read(20, () => 10, () => 30, () => 0);
        Check((double?)shiftedRange["rangeFraction"] == .5, "fraction respects a nonzero minimum");
        Console.WriteLine("Native amount limits regression passed");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
