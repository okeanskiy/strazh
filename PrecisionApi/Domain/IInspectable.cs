namespace PrecisionApi.Domain
{
    public interface IInspectable
    {
        string ToInspection();
    }

    public static class InspectableExtensions 
    {
        public static string Inspect(this string value)
            => value? //deal with null
                .Replace("\\", "\\\\") //escape backslash
                .Replace("\"", "\\\""); //escape quote
    }
} 