using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Firebird.Aspire.Hosting;
public static class FbBuilderExtensions
{
	public static IResourceBuilder<FbServerResource> AddFirebird
	(
		this IDistributedApplicationBuilder builder,
		string name,
		IResourceBuilder<ParameterResource>? user = default,
		IResourceBuilder<ParameterResource>? password = default,
		int? port = default
	)
	{
		ParameterResource passwordParameter = password?.Resource ??
			ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");

		FbServerResource firebird = new(name, user?.Resource, passwordParameter);
		string? connectionString = default;
		builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(firebird, async (e, ct) =>
		{
			connectionString = await firebird.GetConnectionStringAsync(ct).ConfigureAwait(false);
			if (connectionString is null)
			{
				throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{firebird.Name}' resource but the connection string was null.");
			}
		});
		return builder.AddResource(firebird)
			.WithEndpoint(port: port, targetPort: 3050, name: FbServerResource.PrimaryEndpointName)
			.WithImage(FirebirdContainerImageTags.Image, FirebirdContainerImageTags.Tag)
			.WithImageRegistry(FirebirdContainerImageTags.Registry);
	}

	public static IResourceBuilder<FbDatabaseResource> AddDatabase
	(
		this IResourceBuilder<FbServerResource> builder,
		string name,
		string? databaseName = default
	)
	{
		databaseName ??= name;
		builder.Resource.AddDatabase(name, databaseName);
		builder.WithEnvironment("FIREBIRD_DATABASE", databaseName);
		FbDatabaseResource fbDatabase = new(name, databaseName, builder.Resource);

		string? connectionString = default;
		builder.ApplicationBuilder.Eventing.Subscribe<ConnectionStringAvailableEvent>(fbDatabase, async (e, ct) =>
		{
			connectionString = await fbDatabase.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
			if (connectionString is null)
			{
				throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{fbDatabase.Name}' resource but the connection string was null.");
			}
		});
		return builder.ApplicationBuilder.AddResource(fbDatabase);
	}
}
