namespace SSLTrack.Extensions;

public static class Expiration
{
    public static bool GetExpirationStatus(DateTime domainExpiration, int daysToExpiration)
    {
        if (domainExpiration < DateTime.Now.AddDays(daysToExpiration))
        {
            return true;
        }
        return false;
    }
}
