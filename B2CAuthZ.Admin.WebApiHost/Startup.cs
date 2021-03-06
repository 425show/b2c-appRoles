using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.Graph;

namespace B2CAuthZ.Admin.WebApiHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(Configuration.GetSection("AzureAdB2C"));

            services.AddHttpContextAccessor(); // should already be here due to microsoft.identity.web above
            services.AddOptions<AzureAdAdminConfiguration>()
                .Configure<IConfiguration>((opt, config) =>
                {
                    config.GetSection("AzureAdDirectoryAdmin").Bind(opt);
                });
            services.AddSingleton<IAuthenticationProvider, MsalTokenProvider>();

            // todo: you gotta be kidding me;
            // this is for GraphServiceClient's init, which expects a base url as string o_O
            services.AddSingleton<string>("https://graph.microsoft.com/v1.0/");
            services.AddSingleton<GraphServiceClient>();
            services.AddOptions<OrganizationOptions>()
                .Configure<IConfiguration>((opt, config) =>
                {
                    config.GetSection("OrganizationOptions").Bind(opt);
                });

            // these are very important to stay scoped - as they are configured _per request_ with the orgId
            services.AddScoped<IUserRepository, OrganizationFilteredUserRepository>();
            services.AddScoped<IApplicationRepository, OrganizationFilteredApplicationRepository>();

            services.AddControllers().AddJsonOptions(x =>
            {
                // used to keep graph objects small
                x.JsonSerializerOptions.IgnoreNullValues = true;
                x.JsonSerializerOptions.MaxDepth = 5;
            });

            services.AddApiVersioning();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1.0", new OpenApiInfo { Title = "B2C Authorization Administration", Version = "v1.0" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1.0/swagger.json", "B2C Authorization Administration v1.0");
                    c.RoutePrefix = string.Empty;
                    c.DefaultModelExpandDepth(1);
                });
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
