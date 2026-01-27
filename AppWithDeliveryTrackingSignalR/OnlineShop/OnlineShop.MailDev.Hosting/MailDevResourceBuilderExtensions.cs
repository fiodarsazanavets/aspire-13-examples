using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace OnlineShop.MailDev.Hosting;

public static class MailDevResourceBuilderExtensions
{
    public static IResourceBuilder<MailDevResource> AddMailDev(
        this IDistributedApplicationBuilder builder,
        string name,
        int? httpPort = null,
        int? smtpPort = null)
    {
        MailDevResource resource = new(name);

        return builder.AddResource(resource)
              .WithImage(MailDevContainerImageTags.Image)
              .WithImageRegistry(MailDevContainerImageTags.Registry)
              .WithImageTag(MailDevContainerImageTags.Tag)
              .WithHttpEndpoint(
                  targetPort: 1080,
                  port: httpPort,
                  name: MailDevResource.HttpEndpointName)
              .WithEndpoint(
                  targetPort: 1025,
                  port: smtpPort,
                  name: MailDevResource.SmtpEndpointName);

    }

}
