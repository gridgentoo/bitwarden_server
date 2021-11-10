namespace Bit.Core.Models.Mail.FamiliesForEnterprise
{
    public class FamiliesForEnterpriseOfferViewModel : BaseMailModel
    {
        public string SponsorshipToken { get; set; }
        public string Url => $"{WebVaultUrl}/sponsored/families-for-enterprise?token={SponsorshipToken}";
    }
}