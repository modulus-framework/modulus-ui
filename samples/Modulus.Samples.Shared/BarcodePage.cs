namespace Modulus.Samples.Shared;

/// <summary>The barcode sample's values: a shipment (Code 128), a product (EAN-13), digit pairs (Code 128 set C), and a product number with a wrong check digit.</summary>
public static class BarcodePage
{
    public const string Shipment = "SHP-2026-000471";
    public const string Product = "5901234123457";
    public const string Digits = "0123456789";
    public const string WrongCheck = "5901234123458";
}
