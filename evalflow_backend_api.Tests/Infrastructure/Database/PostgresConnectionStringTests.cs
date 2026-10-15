using evalflow_backend_api.Infrastructure.Database;
using Npgsql;

namespace evalflow_backend_api.Tests.Infrastructure.Database;

public class PostgresConnectionStringTests
{
    [Fact]
    public void ANeonUrl_BecomesAnNpgsqlConnectionString()
    {
        var result = PostgresConnectionString.Normalize(
            "postgresql://neondb_owner:p%40ss@ep-green-wind-b2bq5w04-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");

        var builder = new NpgsqlConnectionStringBuilder(result);
        builder.Host.Should().Be("ep-green-wind-b2bq5w04-pooler.eu-central-1.aws.neon.tech");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("neondb");
        builder.Username.Should().Be("neondb_owner");
        builder.Password.Should().Be("p@ss");
        builder.SslMode.Should().Be(SslMode.Require);
        builder.ChannelBinding.Should().Be(ChannelBinding.Require);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=EvalFlowDb;Username=postgres;Password=x")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_IsLeftAsItIs(string? value)
    {
        PostgresConnectionString.Normalize(value).Should().Be(value);
    }
}
