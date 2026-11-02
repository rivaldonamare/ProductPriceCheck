using System.Globalization;

namespace ElectricalStoreAI.Application.Common;

public static class RupiahFormatter
{
    private static readonly CultureInfo IndonesianCulture = CultureInfo.GetCultureInfo("id-ID");

    /// <summary>Formats a price as Indonesian Rupiah; decimals are shown only when the price has a fractional part.</summary>
    public static string Format(decimal amount)
        => amount.ToString(decimal.Truncate(amount) == amount ? "C0" : "C2", IndonesianCulture);
}
