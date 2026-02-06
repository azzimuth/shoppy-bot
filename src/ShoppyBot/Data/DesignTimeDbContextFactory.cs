using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShoppyBot.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShoppyBotContext>
{
    public ShoppyBotContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Database=shoppybot;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ShoppyBotContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ShoppyBotContext(optionsBuilder.Options);
    }
}
