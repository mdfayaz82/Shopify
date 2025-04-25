using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ShopifyTest.Data;
using ShopifyTest;
using Microsoft.Extensions.Options;
using ShopifyTest.Model;
using ShopifyTest.Services;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure DbContext with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSqlConnection"))
           .LogTo(Console.WriteLine, LogLevel.Information)
           .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
);

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var secretKey = builder.Configuration["Jwt:SecretKey"];

        if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException("Jwt:Issuer");
        if (string.IsNullOrEmpty(audience)) throw new ArgumentNullException("Jwt:Audience");
        if (string.IsNullOrEmpty(secretKey)) throw new ArgumentNullException("Jwt:SecretKey");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

// Configure Shopify settings and inject ShopifyServiceFactory
builder.Services.Configure<ShopifySettings>(builder.Configuration.GetSection("Shopify"));
builder.Services.AddScoped<ShopifyServiceFactory>(provider =>
{
    var settings = provider.GetRequiredService<IOptions<ShopifySettings>>().Value;
    var dbContext = provider.GetRequiredService<AppDbContext>();

    if (string.IsNullOrEmpty(settings.ShopUrl))
        throw new ArgumentNullException(nameof(settings.ShopUrl));

    if (string.IsNullOrEmpty(settings.AccessToken))
        throw new ArgumentNullException(nameof(settings.AccessToken));

    return new ShopifyServiceFactory(settings, dbContext);
});

// Add MVC & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Build the app
var app = builder.Build();

// Configure middleware
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
