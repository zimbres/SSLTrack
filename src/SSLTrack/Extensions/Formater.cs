namespace SSLTrack.Extensions;

public static class Formater
{
    public static string FormateIssuer(this string issuer)
    {
        try
        {
            var pattern = @"O=([^,]+)";
            Match match = Regex.Match(issuer, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Replace("\"", "");
            }
            return issuer.Replace("CN=", "");
        }
        catch
        {
            return issuer;
        }
    }
    public static string FormateCN(this string cn)
    {
        try
        {
            var pattern = @"CN=([^,]+)";
            Match match = Regex.Match(cn, pattern);
            return match.Groups[1].Value;
        }
        catch
        {
            return cn;
        }
    }
}
