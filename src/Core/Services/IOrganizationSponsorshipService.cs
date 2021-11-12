using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IOrganizationSponsorshipService
    {
        Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, Organization sponsoringOrg);
        Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName);
        Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship);
        Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship, Organization sponsoredOrganization);
        Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId);
        Task RemoveSponsorshipAsync(Organization sponsoredOrganization, OrganizationSponsorship sponsorship);
    }
}
