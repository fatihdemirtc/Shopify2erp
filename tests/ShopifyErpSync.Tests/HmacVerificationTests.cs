using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ShopifyErpSync.Api.Middleware;

namespace ShopifyErpSync.Tests;

public class HmacVerificationTests
{
    private const string Secret = "test-webhook-secret";

    [Fact]
    public void ComputeHmac_ValidPayload_ReturnsBase64Hash()
    {
        var payload = """{"id":12345,"order_number":"1001"}""";

        var result = HmacVerificationMiddleware.ComputeHmac(payload, Secret);

        result.Should().NotBeNullOrEmpty();
        Convert.TryFromBase64String(result, new byte[256], out _).Should().BeTrue();
    }

    [Fact]
    public void ComputeHmac_MatchesManualComputation()
    {
        var payload = """{"id":12345,"order_number":"1001"}""";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expected = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        var actual = HmacVerificationMiddleware.ComputeHmac(payload, Secret);

        actual.Should().Be(expected);
    }

    [Fact]
    public void ComputeHmac_DifferentSecrets_ProduceDifferentHashes()
    {
        var payload = """{"id":12345}""";

        var hash1 = HmacVerificationMiddleware.ComputeHmac(payload, "secret-one");
        var hash2 = HmacVerificationMiddleware.ComputeHmac(payload, "secret-two");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHmac_DifferentPayloads_ProduceDifferentHashes()
    {
        var hash1 = HmacVerificationMiddleware.ComputeHmac("""{"id":1}""", Secret);
        var hash2 = HmacVerificationMiddleware.ComputeHmac("""{"id":2}""", Secret);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHmac_SameInputs_ProduceSameHash()
    {
        var payload = """{"id":99999}""";

        var hash1 = HmacVerificationMiddleware.ComputeHmac(payload, Secret);
        var hash2 = HmacVerificationMiddleware.ComputeHmac(payload, Secret);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void FixedTimeEquals_ValidHash_Passes()
    {
        var payload = """{"id":12345}""";
        var computed = HmacVerificationMiddleware.ComputeHmac(payload, Secret);

        var provided = Convert.FromBase64String(computed);
        var expected = Convert.FromBase64String(
            HmacVerificationMiddleware.ComputeHmac(payload, Secret));

        CryptographicOperations.FixedTimeEquals(provided, expected).Should().BeTrue();
    }

    [Fact]
    public void FixedTimeEquals_InvalidHash_Fails()
    {
        var payload = """{"id":12345}""";
        var validHmac = HmacVerificationMiddleware.ComputeHmac(payload, Secret);
        var invalidHmac = HmacVerificationMiddleware.ComputeHmac(payload, "wrong-secret");

        var provided = Convert.FromBase64String(invalidHmac);
        var expected = Convert.FromBase64String(validHmac);

        CryptographicOperations.FixedTimeEquals(provided, expected).Should().BeFalse();
    }
}
