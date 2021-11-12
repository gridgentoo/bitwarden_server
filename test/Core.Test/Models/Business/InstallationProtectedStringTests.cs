using System;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Models.Business
{
    public class InstallationProtectedStringTests
    {
        [Theory]
        [BitAutoData]
        public void Encryption_EncryptionRoundTrip_Success(string clearText, Guid password)
        {
            var protString = InstallationProtectedString.Encrypt(clearText, password.ToString());
            var decryptedString = protString.Decrypt(password.ToString());

            Assert.Equal(clearText, decryptedString);
        }

        [Theory]
        [BitAutoData]
        public void Encryption_B64StringRoundTrip_Success(string clearText, Guid password)
        {
            var protString = InstallationProtectedString.Encrypt(clearText, password.ToString());
            var parseProtString = new InstallationProtectedString(protString.EncryptedString);
            var decryptedString = parseProtString.Decrypt(password.ToString());

            Assert.Equal(clearText, decryptedString);
        }
    }
}
