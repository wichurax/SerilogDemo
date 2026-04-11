using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentGateway.Data;

public sealed class PaymentGatewayDbContextFactory : IDesignTimeDbContextFactory<PaymentGatewayDbContext>
{
    public PaymentGatewayDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentGatewayDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=ecommerce;Username=serilog;Password=serilog123";

        optionsBuilder.UseNpgsql(connectionString);
        return new PaymentGatewayDbContext(optionsBuilder.Options);
    }
}