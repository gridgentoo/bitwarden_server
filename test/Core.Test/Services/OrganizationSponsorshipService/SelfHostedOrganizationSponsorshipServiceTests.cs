using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services.OrganizationSponsorshipService
{
    [SutProviderCustomize]
    public class SelfHostedOrganizatioSponsorshipServiceTests
    {
        [Theory]
        [BitAutoData]
        public async Task SendSponsorshipOfferAsync(Organization org, OrganizationSponsorship sponsorship,
            SutProvider<SelfHostedOrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.SendSponsorshipOfferAsync(org, sponsorship);

            await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorshipAsync_NoCloudApiKey_throws(Organization sponsoringOrg,
            SutProvider<SelfHostedOrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.CloudApiKey = null;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, default, default, default, default));

            Assert.Contains("Organization has not enabled self-hosted sponsorships.", exception.Message);
        }
    }
}
