namespace SSLTrackAgent.Models;

public class Domain
{
    public string DomainName { get; set; }
    public int Port { get; set; }
    public string CertCN { get; set; }
    public string Issuer { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime LastChecked { get; set; }
    public string UserId { get; set; }
    public int Agent { get; set; }
    public bool Silenced { get; set; }
    public int Id { get; set; }
    public bool PublicPrefix { get; set; }
}
