using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.WebEncoders;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using SimplCommerce.Infrastructure;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Modules;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.Core.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Localization.Extensions;
using SimplCommerce.Module.Localization.TagHelpers;
using SimplCommerce.WebHost.Extensions;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices();
var app = builder.Build();
Configure();

app.Run();

void ConfigureServices()
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Configuration.AddEntityFrameworkConfig(options =>
    {
        options.UseSqlServer(connectionString);
    });

    GlobalConfiguration.WebRootPath = builder.Environment.WebRootPath;
    GlobalConfiguration.ContentRootPath = builder.Environment.ContentRootPath;

    // 1. Modules & Data Store
    builder.Services.AddModules();
    builder.Services.AddCustomizedDataStore(builder.Configuration);

    // 2. Identity + IdentityServer (COOKIE AUTH – REQUIRED)
    builder.Services.AddCustomizedIdentity(builder.Configuration);

    // 3. JWT Bearer (API ONLY – NOT DEFAULT)
    builder.Services.AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "SealcoLg",
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
                )
            };

            options.Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync(
                        "{\"error\":\"Unauthorized - Bearer token required\"}");
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine("JWT Auth Failed: " + context.Exception.Message);
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            };
        });

    // 4. Standard Services
    builder.Services.AddHttpClient();
    builder.Services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddTransient(typeof(IRepositoryWithTypedId<,>), typeof(RepositoryWithTypedId<,>));
    builder.Services.AddScoped<SlugRouteValueTransformer>();

    builder.Services.AddCustomizedLocalization();
    builder.Services.AddCustomizedMvc(GlobalConfiguration.Modules);

    builder.Services.Configure<RazorViewEngineOptions>(options =>
        options.ViewLocationExpanders.Add(new ThemeableViewLocationExpander()));

    builder.Services.Configure<WebEncoderOptions>(options =>
    {
        options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
    });

    builder.Services.AddScoped<ITagHelperComponent, LanguageDirectionTagHelperComponent>();
    builder.Services.AddTransient<IRazorViewRenderer, RazorViewRenderer>();
    builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-Token");
    builder.Services.AddCloudscribePagination();
    builder.Services.ConfigureModules();

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // 5. CORS (Next.js)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowNextJS", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://sealco-leb.com:3000", "https://sealco-leb.vercel.app")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // 6. Swagger (JWT)
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SimplCommerce API",
            Version = "v1"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            Description = "Authorization header using Bearer scheme. Example: \"Bearer {token}\""
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

void Configure()
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseMigrationsEndPoint();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SimplCommerce API V1");
            c.RoutePrefix = "swagger";
        });
    }
    else
    {
        app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
            a => a.UseExceptionHandler("/Home/Error"));
        app.UseHsts();
    }

    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
        a => a.UseStatusCodePagesWithReExecute("/Home/ErrorWithCode/{0}"));

    app.UseHttpsRedirection();
    app.UseCors("AllowNextJS");
    app.UseCustomizedStaticFiles(builder.Environment);

    app.UseRouting();
    app.UseCookiePolicy();
    // 🔴 ORDER IS CRITICAL
    app.UseIdentityServer();   // Cookies & sessions
    app.UseAuthentication();   // JWT validation
    app.UseAuthorization();

    app.UseCustomizedRequestLocalization();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapDynamicControllerRoute<SlugRouteValueTransformer>("/{**slug}");

        endpoints.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        endpoints.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
    });

    foreach (var initializer in app.Services.GetServices<IModuleInitializer>())
    {
        initializer.Configure(app, builder.Environment);
    }
}
