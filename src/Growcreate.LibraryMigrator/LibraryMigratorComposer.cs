using Growcreate.LibraryMigrator.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Growcreate.LibraryMigrator;

public class LibraryMigratorComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<LibraryMigratorSettings>(
            builder.Config.GetSection("Growcreate.LibraryMigrator"));
        builder.Services.AddTransient<IElementMigrationService, ElementMigrationService>();
        builder.Services.AddTransient<IGlobalMigrationService, ElementMigrationService>();
    }
}
