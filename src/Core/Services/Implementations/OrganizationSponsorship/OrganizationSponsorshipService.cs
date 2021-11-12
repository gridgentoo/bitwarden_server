using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public abstract class OrganizationSponsorshipService : IOrganizationSponsorshipService
    {
        protected const string FamiliesForEnterpriseTokenName = "FamiliesForEnterpriseToken";
        protected const string TokenClearTextPrefix = "BWOrganizationSponsorship_";

        protected readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        protected readonly IOrganizationRepository _organizationRepository;
        protected readonly IPaymentService _paymentService;
        protected readonly IMailService _mailService;

        public OrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService)
        {
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _paymentService = paymentService;
            _mailService = mailService;
        }

        public abstract Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, Organization sponsoringOrg);

        protected virtual string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType,
            Organization sponsoringOrg) => $"{FamiliesForEnterpriseTokenName} {sponsorshipId} {sponsorshipType}";

        public virtual async Task OfferSponsorshipAsync(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            PlanSponsorshipType sponsorshipType, string sponsoredEmail, string friendlyName)
        {
            var sponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = sponsorshipType,
                CloudSponsor = true,
            };

            try
            {
                sponsorship = await _organizationSponsorshipRepository.CreateAsync(sponsorship);

                await SendSponsorshipOfferAsync(sponsoringOrg, sponsorship);
            }
            catch
            {
                if (sponsorship.Id != default)
                {
                    await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
                }
                throw;
            }
        }

        public abstract Task SendSponsorshipOfferAsync(Organization sponsoringOrg, OrganizationSponsorship sponsorship);

        public virtual async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
            Organization sponsoredOrganization)
        {
            sponsorship.SponsoredOrganizationId = sponsoredOrganization.Id;
            sponsorship.OfferedToEmail = null;
            await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
        }

        public abstract Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId);

        public abstract Task RemoveSponsorshipAsync(Organization sponsoredOrganization,
            OrganizationSponsorship sponsorship = null);
    }
}
