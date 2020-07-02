using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation.Processors.Security;

namespace SwaggerAzureADWebAPIAuthentication
{
    public class Startup
    {
        public Startup (IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices (IServiceCollection services)
        {
            services.AddControllers();

            // Enable JWT Bearer Authentication
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options => 
            { 
                Configuration.Bind("AzureAd", options);
                // Authority will be Your AzureAd Instance and Tenant Id
                options.Authority = $"{Configuration["AzureAd:Instance"]}{Configuration["AzureAd:TenantId"]}/v2.0";

                // The valid audiences are both the Client ID(options.Audience) and api://{ClientID}
                options.TokenValidationParameters.ValidAudiences = new string[] { Configuration["AzureAd:ClientId"], $"api://{Configuration["AzureAd:ClientId"]}" };
            }) ;

            AddSwagger(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure (IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Add Swagger UI
            app.UseOpenApi();
            app.UseSwaggerUi3(settings =>
            {
                settings.OAuth2Client = new OAuth2ClientSettings
                {
                    // Use the same client id as your application.
                    // Alternatively you can register another application in the portal and use that as client id
                    // Doing that you will have to create a client secret to access that application and get into space of secret management
                    // This makes it easier to access the application and grab a token on behalf of user
                    ClientId = Configuration["AzureAd:ClientId"],
                    AppName = "SubHub-Swagger-UI-Client",
                };
            });
        }

        /// <summary>
        /// Function to Generate Swagger UI Document and authenticate Swagger UI against the Azure Ad Application
        /// </summary>
        /// <param name="services"></param>
        private void AddSwagger (IServiceCollection services)
        {
            services.AddOpenApiDocument(document =>
            {
                document.AddSecurity("bearer", Enumerable.Empty<string>(), new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.OAuth2,
                    Description = "Azure AAD Authentication",
                    Flow = OpenApiOAuth2Flow.Implicit,
                    Flows = new OpenApiOAuthFlows()
                    {
                        Implicit = new OpenApiOAuthFlow()
                        {
                            Scopes = new Dictionary<string, string>
                        {
                            { $"api://{Configuration["AzureAd:ClientId"]}/user_impersonation", "Access Application" },
                        },
                            AuthorizationUrl = $"{Configuration["AzureAd:Instance"]}{Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize",
                            TokenUrl = $"{Configuration["AzureAd:Instance"]}{Configuration["AzureAd:TenantId"]}/oauth2/v2.0/token",                            
                        },
                    },
                });

                // To add bearer token in request to APIs with Authorize attribute
                document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
            });
        }
    }
}
