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
    public class CloudOrganizationSponsorshipService : OrganizationSponsorshipService, IOrganizationSponsorshipService
    {
        private readonly IDataProtector _dataProtector;

        public CloudOrganizationSponsorshipService(IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IPaymentService paymentService,
            IMailService mailService,
            IDataProtectionProvider dataProtectionProvider) :
            base(organizationSponsorshipRepository, organizationRepository, paymentService, mailService)
        {
            _dataProtector = dataProtectionProvider.CreateProtector("OrganizationSponsorshipServiceDxataProtector");
        }

        public override async Task<bool> ValidateRedemptionTokenAsync(string token, Organization sponsoringOrg)
        {
            if (!token.StartsWith(TokenClearTextPrefix))
            {
                return false;
            }
            var encryptedToken = token[TokenClearTextPrefix.Length..];

            var decryptedToken = encryptedToken.StartsWith("Self-hosted") ?
                new InstallationProtectedString(encryptedToken["Self-hosted".Length..]).Decrypt(sponsoringOrg.ApiKey) :
                _dataProtector.Unprotect(encryptedToken);
            var dataParts = decryptedToken.Split(' ');

            if (dataParts.Length != 3)
            {
                return false;
            }

            if (dataParts[0].Equals(FamiliesForEnterpriseTokenName))
            {
                if (!Guid.TryParse(dataParts[1], out Guid sponsorshipId) ||
                    !Enum.TryParse<PlanSponsorshipType>(dataParts[2], true, out var sponsorshipType))
                {
                    return false;
                }

                var sponsorship = await _organizationSponsorshipRepository.GetByIdAsync(sponsorshipId);
                if (sponsorship == null || sponsorship.PlanSponsorshipType != sponsorshipType)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        protected override string RedemptionToken(Guid sponsorshipId, PlanSponsorshipType sponsorshipType,
            Organization sponsoringOrg) =>
            string.Concat(
                TokenClearTextPrefix,
                _dataProtector.Protect(base.RedemptionToken(sponsorshipId, sponsorshipType, sponsoringOrg))
            );

        public override async Task SendSponsorshipOfferAsync(Organization sponsoringOrg,
            OrganizationSponsorship sponsorship)
        {
            await _mailService.SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, sponsoringOrg.Name,
                RedemptionToken(sponsorship.Id, sponsorship.PlanSponsorshipType.Value, sponsoringOrg));
        }

        public override async Task SetUpSponsorshipAsync(OrganizationSponsorship sponsorship,
            Organization sponsoredOrganization)
        {
            if (sponsorship.PlanSponsorshipType == null)
            {
                throw new BadRequestException("Cannot set up sponsorship without a known sponsorship type.");
            }

            // TODO: rollback?
            await _paymentService.SponsorOrganizationAsync(sponsoredOrganization, sponsorship);
            await _organizationRepository.UpsertAsync(sponsoredOrganization);

            await base.SetUpSponsorshipAsync(sponsorship, sponsoredOrganization);
        }

        public override async Task<bool> ValidateSponsorshipAsync(Guid sponsoredOrganizationId)
        {
            var sponsoredOrganization = await _organizationRepository.GetByIdAsync(sponsoredOrganizationId);
            if (sponsoredOrganization == null)
            {
                return false;
            }

            var existingSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrganizationId);

            if (existingSponsorship == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, null);
                return false;
            }

            if (existingSponsorship.SponsoringOrganizationId == null ||
                existingSponsorship.SponsoringOrganizationUserId == null ||
                existingSponsorship.PlanSponsorshipType == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }
            var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(existingSponsorship.PlanSponsorshipType.Value);

            var sponsoringOrganization = await _organizationRepository
                .GetByIdAsync(existingSponsorship.SponsoringOrganizationId.Value);
            if (sponsoringOrganization == null)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            var sponsoringOrgPlan = Utilities.StaticStore.GetPlan(sponsoringOrganization.PlanType);
            if (!sponsoringOrganization.Enabled || sponsoredPlan.SponsoringProductType != sponsoringOrgPlan.Product)
            {
                await RemoveSponsorshipAsync(sponsoredOrganization, existingSponsorship);
                return false;
            }

            return true;
        }

        public override async Task RemoveSponsorshipAsync(Organization sponsoredOrganization,
            OrganizationSponsorship sponsorship = null)
        {
            if (sponsoredOrganization != null)
            {
                await _paymentService.RemoveOrganizationSponsorshipAsync(sponsoredOrganization, sponsorship);
                await _organizationRepository.UpsertAsync(sponsoredOrganization);

                await _mailService.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(
                    sponsoredOrganization.BillingEmailAddress(),
                    sponsoredOrganization.Name);
            }

            if (sponsorship == null)
            {
                return;
            }

            // Initialize the record as available
            sponsorship.SponsoredOrganizationId = null;
            sponsorship.FriendlyName = null;
            sponsorship.OfferedToEmail = null;
            sponsorship.PlanSponsorshipType = null;
            sponsorship.TimesRenewedWithoutValidation = 0;
            sponsorship.SponsorshipLapsedDate = null;

            if (sponsorship.CloudSponsor || sponsorship.SponsorshipLapsedDate.HasValue)
            {
                await _organizationSponsorshipRepository.DeleteAsync(sponsorship);
            }
            else
            {
                await _organizationSponsorshipRepository.UpsertAsync(sponsorship);
            }
        }
    }
}
