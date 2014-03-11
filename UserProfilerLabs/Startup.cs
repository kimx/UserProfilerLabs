using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(UserProfilerLab.Startup))]
namespace UserProfilerLab
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
