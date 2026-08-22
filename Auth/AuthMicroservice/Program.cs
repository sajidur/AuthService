using AuthMicroservice;
using AuthMicroservice.Repository;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder => builder
            .AllowAnyOrigin()// Allow these origins
            .AllowAnyHeader() // Allow any headers
            .AllowAnyMethod()); // Allow any HTTP methods
}); 

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<UserDbContext>();

builder.Services.AddControllers();
builder.Services.AddApplicationServices();

// JWT auth. Each tenant (Application row) has its own AppSecret (signing key), Audience,
// and Issuer (see UserService.GetToken) rather than one shared value for the whole
// system, so validation resolves all three dynamically per-request from the AppKey
// header via ITenantKeyProvider (registered in AddApplicationServices), instead of
// static config.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<ITenantKeyProvider, IHttpContextAccessor>((options, tenantKeyProvider, httpContextAccessor) =>
    {
        Application ResolveTenant() =>
            tenantKeyProvider.ResolveByAppKey(httpContextAccessor.HttpContext?.Request.Headers["AppKey"].FirstOrDefault());

        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            var app = ResolveTenant();
            if (app == null || string.IsNullOrEmpty(app.AppSecret))
                return Enumerable.Empty<SecurityKey>();

            return new SecurityKey[] { new SymmetricSecurityKey(Encoding.ASCII.GetBytes(app.AppSecret)) };
        };

        options.TokenValidationParameters.IssuerValidator = (issuer, securityToken, validationParameters) =>
        {
            var app = ResolveTenant();
            if (app != null && string.Equals(issuer, app.Issuer, StringComparison.Ordinal))
                return issuer;

            throw new SecurityTokenInvalidIssuerException("Token issuer does not match the tenant resolved from the AppKey header.") { InvalidIssuer = issuer };
        };

        options.TokenValidationParameters.AudienceValidator = (audiences, securityToken, validationParameters) =>
        {
            var app = ResolveTenant();
            return app != null && audiences.Contains(app.Audience);
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHostedService<CampaignSchedulerService>();
builder.Services.AddHostedService<ImapPollingService>();
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(8, 0, 21))),ServiceLifetime.Scoped);

var app = builder.Build();
app.UseCors("AllowSpecificOrigins");

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });
//}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
{
    var context = serviceScope.ServiceProvider.GetRequiredService<UserDbContext>();
    //context.Database.Migrate();
}
app.Run();
