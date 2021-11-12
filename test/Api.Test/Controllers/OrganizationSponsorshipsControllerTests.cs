using Xunit;
using Bit.Test.Common.AutoFixture.Attributes;
using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Test.Common.AutoFixture;
using Bit.Api.Controllers;
using Bit.Core.Context;
using NSubstitute;
using Bit.Core.Exceptions;
using Bit.Api.Test.AutoFixture.Attributes;
using Bit.Core.Repositories;
using Bit.Core.Models.Api.Request;
using Bit.Core.Services;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.Test.Controllers
{
    [ControllerCustomize(typeof(OrganizationSponsorshipsController))]
    [SutProviderCustomize]
    public class OrganizationSponsorshipsControllerTests
    {
        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product == ProductType.Enterprise).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Enterprise).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonFamiliesPlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Families).Select(p => new object[] { p });

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan, Organization org,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;
            model.PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
            Enum.GetValues<OrganizationUserStatusType>()
                .Where(s => s != OrganizationUserStatusType.Confirmed)
                .Select(s => new object[] { s });

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorship_AlreadySponsoring_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsoringOrgNotFound_ThrowsBadRequest(Guid sponsoringOrgId,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendSponsorshipOffer(sponsoringOrgId.ToString()));

            Assert.Contains("Cannot find the requested sponsoring organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsoringOrgUserNotFound_ThrowsBadRequest(Organization org,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendSponsorshipOffer(org.Id.ToString()));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task ResendSponsorshipOffer_SponsoringOrgUserNotConfirmed_ThrowsBadRequest(OrganizationUserStatusType status,
            Organization org, OrganizationUser orgUser,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            orgUser.Status = status;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendSponsorshipOffer(org.Id.ToString()));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsorshipNotFound_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendSponsorshipOffer(org.Id.ToString()));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_NoOfferToEmail_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            sponsorship.OfferedToEmail = null;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendSponsorshipOffer(org.Id.ToString()));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_SponsorshipNotFound_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_SponsoringOrgNotFound_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Sponsor offer is invalid, cannot find the sponsoring organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task RedeemSponsorship_SponsoringOrgNotCorrectType_ThrowsBadRequest(PlanType planType,
            string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model, User user,
            OrganizationSponsorship sponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task RedeemSponsorship_BadToken_ThrowsBadRequest(PlanType planType,
            string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model, User user,
            OrganizationSponsorship sponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(default, default)
                .ReturnsForAnyArgs(false);


            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Failed to parse sponsorship token.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task RedeemSponsorship_NotSponsoredOrgOwner_ThrowsBadRequest(PlanType planType,
            string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model, User user,
            OrganizationSponsorship sponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(default, default)
                .ReturnsForAnyArgs(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship for an organization you own.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task RedeemSponsorship_OrgAlreadySponsored_ThrowsBadRequest(PlanType planType,
            string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model, User user,
            OrganizationSponsorship sponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;
            user.Email = sponsorship.OfferedToEmail;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(default, default)
                .ReturnsForAnyArgs(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonFamiliesPlanTypes))]
        public async Task RedeemSponsorship_OrgNotFamiles_ThrowsBadRequest(PlanType planType,
            string sponsorshipToken, OrganizationSponsorshipRedeemRequestModel model, User user,
            OrganizationSponsorship sponsorship, Organization sponsoringOrg, Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
            sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;
            sponsoredOrg.PlanType = planType;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(user.Email).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(default, default)
                .ReturnsForAnyArgs(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.SponsoredOrganizationId).Returns(sponsoredOrg);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_WrongSponsoringUser_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
            Guid currentUserId, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsoringOrgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id.ToString()));

            Assert.Contains("Can only revoke a sponsorship you granted.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(OrganizationUser orgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgUser.OrganizationId, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != orgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns((OrganizationSponsorship)null);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorship(orgUser.OrganizationId.ToString()));

            Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsorshipNotRedeemed_ThrowsBadRequest(OrganizationUser orgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgUser.OrganizationId, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != orgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns((OrganizationSponsorship)sponsorship);

            await sutProvider.Sut.RevokeSponsorship(orgUser.OrganizationId.ToString());

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1).DeleteAsync(sponsorship);

            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_WrongOrgUserType_ThrowsBadRequest(Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("Only the owner of an organization can remove sponsorship.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_NotSponsored_ThrowsBadRequest(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(Arg.Is<Guid>(v => v != sponsoredOrg.Id))
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(Organization sponsoredOrg,
    OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }
    }
}
