using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Api.Request;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("organization/sponsorship")]
    [Authorize("Application")]
    public class OrganizationSponsorshipsController : Controller
    {
        private readonly IOrganizationSponsorshipService _organizationsSponsorshipService;
        private readonly IOrganizationSponsorshipRepository _organizationSponsorshipRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICurrentContext _currentContext;
        private readonly IUserService _userService;

        public OrganizationSponsorshipsController(IOrganizationSponsorshipService organizationSponsorshipService,
            IOrganizationSponsorshipRepository organizationSponsorshipRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IUserService userService,
            ICurrentContext currentContext)
        {
            _organizationsSponsorshipService = organizationSponsorshipService;
            _organizationSponsorshipRepository = organizationSponsorshipRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task CreateSponsorship(string sponsoringOrgId, [FromBody] OrganizationSponsorshipRequestModel model)
        {
            // TODO: validate has right to sponsor, send sponsorship email
            var sponsoringOrgIdGuid = new Guid(sponsoringOrgId);
            var requiredSponsoringProductType = StaticStore.GetSponsoredPlan(model.PlanSponsorshipType)?.SponsoringProductType;
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgIdGuid);
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgIdGuid, _currentContext.UserId ?? default);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Can only sponsor one organization per Organization User.");
            }

            await _organizationsSponsorshipService.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                model.PlanSponsorshipType, model.SponsoredEmail, model.FriendlyName);
        }

        [HttpPost("{sponsoringOrgId}/families-for-enterprise/resend")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task ResendSponsorshipOffer(string sponsoringOrgId)
        {
            // TODO: validate has right to sponsor, send sponsorship email
            var sponsoringOrgIdGuid = new Guid(sponsoringOrgId);
            var sponsoringOrg = await _organizationRepository.GetByIdAsync(sponsoringOrgIdGuid);
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Cannot find the requested sponsoring organization.");
            }

            var sponsoringOrgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrgIdGuid, _currentContext.UserId ?? default);
            if (sponsoringOrgUser == null || sponsoringOrgUser.Status != OrganizationUserStatusType.Confirmed)
            {
                throw new BadRequestException("Only confirmed users can sponsor other organizations.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository.GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id);
            if (existingOrgSponsorship == null || existingOrgSponsorship.OfferedToEmail == null)
            {
                throw new BadRequestException("Cannot find an outstanding sponsorship offer for this organization.");
            }

            await _organizationsSponsorshipService.SendSponsorshipOfferAsync(sponsoringOrg, existingOrgSponsorship);
        }

        [HttpPost("redeem")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RedeemSponsorship([FromQuery] string sponsorshipToken, [FromBody] OrganizationSponsorshipRedeemRequestModel model)
        {
            var existingSponsorshipOffer = await _organizationSponsorshipRepository
                .GetByOfferedToEmailAsync((await CurrentUser).Email);
            if (existingSponsorshipOffer == null)
            {
                throw new BadRequestException("No unredeemed sponsorship offer exists for you.");
            }

            var sponsoringOrg = await _organizationRepository
                .GetByIdAsync(existingSponsorshipOffer.SponsoringOrganizationId ?? default);
            if (sponsoringOrg == null)
            {
                throw new BadRequestException("Sponsor offer is invalid, cannot find the sponsoring organization.");
            }

            var requiredSponsoringProductType = StaticStore
                .GetSponsoredPlan(existingSponsorshipOffer.PlanSponsorshipType.Value)?.SponsoringProductType;
            if (requiredSponsoringProductType == null ||
                sponsoringOrg == null ||
                StaticStore.GetPlan(sponsoringOrg.PlanType).Product != requiredSponsoringProductType.Value)
            {
                throw new BadRequestException("Specified Organization cannot sponsor other organizations.");
            }

            if (!await _organizationsSponsorshipService.ValidateRedemptionTokenAsync(sponsorshipToken, sponsoringOrg))
            {
                throw new BadRequestException("Failed to parse sponsorship token.");
            }

            if (!await _currentContext.OrganizationOwner(model.SponsoredOrganizationId))
            {
                throw new BadRequestException("Can only redeem sponsorship for an organization you own.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId);
            if (existingOrgSponsorship != null)
            {
                throw new BadRequestException("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.");
            }

            // Check org to sponsor's product type
            var requiredSponsoredProductType = StaticStore.GetSponsoredPlan(model.PlanSponsorshipType)?.SponsoredProductType;
            var organizationToSponsor = await _organizationRepository.GetByIdAsync(model.SponsoredOrganizationId);
            if (requiredSponsoredProductType == null ||
                organizationToSponsor == null ||
                StaticStore.GetPlan(organizationToSponsor.PlanType).Product != requiredSponsoredProductType.Value)
            {
                throw new BadRequestException("Can only redeem sponsorship offer on families organizations.");
            }

            await _organizationsSponsorshipService.SetUpSponsorshipAsync(existingSponsorshipOffer, organizationToSponsor);
        }

        [HttpDelete("{sponsoringOrganizationId}")]
        [HttpPost("{sponsoringOrganizationId}/delete")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RevokeSponsorship(string sponsoringOrganizationId)
        {
            var sponsoringOrganizationIdGuid = new Guid(sponsoringOrganizationId);

            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(sponsoringOrganizationIdGuid, _currentContext.UserId ?? default);
            if (_currentContext.UserId != orgUser?.UserId)
            {
                throw new BadRequestException("Can only revoke a sponsorship you granted.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id);
            if (existingOrgSponsorship == null)
            {
                throw new BadRequestException("You are not currently sponsoring an organization.");
            }
            if (existingOrgSponsorship.SponsoredOrganizationId == null)
            {
                await _organizationSponsorshipRepository.DeleteAsync(existingOrgSponsorship);
                return;
            }

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);
            await _organizationsSponsorshipService.RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        [HttpDelete("sponsored/{sponsoredOrgId}")]
        [HttpPost("sponsored/{sponsoredOrgId}/remove")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task RemoveSponsorship(string sponsoredOrgId)
        {
            var sponsoredOrgIdGuid = new Guid(sponsoredOrgId);

            if (!await _currentContext.OrganizationOwner(sponsoredOrgIdGuid))
            {
                throw new BadRequestException("Only the owner of an organization can remove sponsorship.");
            }

            var existingOrgSponsorship = await _organizationSponsorshipRepository
                .GetBySponsoredOrganizationIdAsync(sponsoredOrgIdGuid);
            if (existingOrgSponsorship == null || existingOrgSponsorship.SponsoredOrganizationId == null)
            {
                throw new BadRequestException("The requested organization is not currently being sponsored.");
            }

            var sponsoredOrganization = await _organizationRepository
                .GetByIdAsync(existingOrgSponsorship.SponsoredOrganizationId.Value);
            if (sponsoredOrganization == null)
            {
                throw new BadRequestException("Unable to find the sponsored Organization.");
            }


            await _organizationsSponsorshipService.RemoveSponsorshipAsync(sponsoredOrganization, existingOrgSponsorship);
        }

        private Task<User> CurrentUser => _userService.GetUserByIdAsync(_currentContext.UserId.Value);
    }
}
