using XYZ.Billing.Api.ErrorHandling;
using Microsoft.EntityFrameworkCore;
using XYZ.Billing.Application.Orders;
using XYZ.Billing.Application.Payments;
using XYZ.Billing.Domain.Repositories;
using XYZ.Billing.Infrastructure.Payments;
using XYZ.Billing.Persistence;
using XYZ.Billing.Persistence.Repositories;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

string databasePath = Path.Combine(builder.Environment.ContentRootPath, "billing.db");
builder.Services.AddDbContext<BillingDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSingleton<IPaymentGateway, MockSuccessPaymentGateway>();
builder.Services.AddSingleton<IPaymentGateway, MockFailurePaymentGateway>();

builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddScoped<IOrderRepository, OrderRepository>();

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    BillingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.MapControllers();

app.Run();