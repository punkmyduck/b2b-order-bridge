using System.Text.Json.Serialization;
using OrderBridge.Application;
using OrderBridge.Presentation.ExceptionHandlers;
using OrderBridge.Infrastructure;

namespace OrderBridge.Presentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"]);
            builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.AddServices();
            builder.Services.AddPersistence(builder.Configuration);
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment() &&
                builder.Configuration.GetValue<bool>("Persistence:ApplyMigrationsOnStartup"))
            {
                await app.Services.ApplyPersistenceMigrationsAsync();
            }

            app.UseExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
