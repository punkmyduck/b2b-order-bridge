using OrderBridge.Infrastructure;
using OrderBridge.Application.Features.Orders.Commands;

namespace OrderBridge.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddMediatR(options =>
            {
                options.Lifetime = ServiceLifetime.Scoped;
                options.RegisterServicesFromAssemblyContaining<CreateOrderCommandHandler>();
                options.LicenseKey = builder.Configuration["MediatR:LicenseKey"];
            });
            builder.Services.AddServices();
            builder.Services.AddPersistence(
                builder.Configuration.GetConnectionString("OrderBridge")
                ?? throw new InvalidOperationException("ConnectionStrings:OrderBridge is required."));
            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}


