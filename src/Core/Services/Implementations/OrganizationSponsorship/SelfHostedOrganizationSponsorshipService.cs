using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public class SelfHostedOrganizationSponsorshipService : OrganizationSponsorshipService
    {
        public SelfHostedOrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService) :
            base(organizationSponsorshipRepository, organizationRepository, paymentService, mailService)
        { }

        public override Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, Organization sponsoringOrg) =>
            throw new NotImplementedException();

        protected override string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType, Organization sponsoringOrg) =>
            string.Concat(
                TokenClearTextPrefix,
                "Self-hosted",
                InstallationProtectedString.Encrypt(base.RedemptionToken(sponsorshipId, sponsorshipType, sponsoringOrg), sponsoringOrg.CloudApiKey)
            );

        public override async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName)
        {
            if (string.IsNullOrWhiteSpace(sponsoringOrg.CloudApiKey))
            {
                throw new BadRequestException("Organization has not enabled self-hosted sponsorships.");
            }
            await base.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser, sponsorshipType, sponsoredEmail,
                friendlyName);
        }

        public override Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship)
        {
            return Task.FromResult(0);
        }

        public override Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId) => throw new NotImplementedException();

        public override async Task RemoveSponsorshipAsync(Organization sponsoredOrganization,
            OrganizationSponsorship sponsorship = null)
        {
            if (sponsorship == null)
            {
                return;
            }

            await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
        }
    }
}
