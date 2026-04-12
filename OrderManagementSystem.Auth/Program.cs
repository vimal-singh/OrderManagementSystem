using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderManagementSystem.Auth.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// add database context for Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AuthenticationServerDBConnection")));
            // Adding ASP.NET Core Identity services.
            builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

builder.Services.AddControllers();

builder.Services.AddAuthentication(options =>
            {
                //Default Authentication Scheme
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            // Adding the JwtBearer authentication handler to validate incoming JWT tokens.
            .AddJwtBearer(options =>
            {
                // Configuring the parameters for JWT token validation.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, // Ensure the token's issuer matches the expected issuer.
                    ValidateAudience = false, // Ensure the token's audience matches the expected audience.
                    ValidateLifetime = true, // Validate that the token has not expired.
                    ValidateIssuerSigningKey = true, // Ensure the token is signed by a trusted signing key.
                    ValidIssuer = builder.Configuration["Jwt:Issuer"], // The expected issuer, retrieved from configuration (appsettings.json).	
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty)) // The symmetric key used to sign the JWT, also from configuration (appsettings.json).
                };
            });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
// Enable authorization middleware.
app.UseAuthorization();
app.MapControllers();

app.Run();
