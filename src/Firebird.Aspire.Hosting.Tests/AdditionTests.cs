using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace Firebird.Aspire.Hosting.Tests;

public class AdditionTests
{
	[Fact]
	public void AddsGeneratedPasswordParameterWithUserSecretsParameterDefault()
	{
		IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

		IResourceBuilder<FbServerResource> firebird = builder.AddFirebird("firebird");

		firebird.Resource.PasswordParameter.Default.Should().NotBeNull();
		firebird.Resource.PasswordParameter.Default.GetType().FullName
			.Should().Be("Aspire.Hosting.ApplicationModel.UserSecretsParameterDefault");
	}

	[Fact]
	public void ContainerWithDefaultsAddsAnnotationMetadata()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder.AddFirebird("firebird");

		using DistributedApplication app = appBuilder.Build();

		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource containerResource = appModel.Resources.OfType<FbServerResource>().Single();
		containerResource.Name.Should().Be("firebird");

		EndpointAnnotation? endpoint = containerResource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault();
		endpoint.Should().NotBeNull();
		endpoint.TargetPort.Should().Be(3050);
		endpoint.IsExternal.Should().BeFalse();
		endpoint.Name.Should().Be("tcp");
		endpoint.Port.Should().BeNull();
		endpoint.Protocol.Should().Be(ProtocolType.Tcp);
		endpoint.Transport.Should().Be("tcp");
		endpoint.UriScheme.Should().Be("tcp");

		ContainerImageAnnotation? containerAnnotation = containerResource.Annotations
			.OfType<ContainerImageAnnotation>()
			.SingleOrDefault();
		containerAnnotation.Should().NotBeNull();
		containerAnnotation.Tag.Should().Be(FirebirdContainerImageTags.Tag);
		containerAnnotation.Image.Should().Be(FirebirdContainerImageTags.Image);
		containerAnnotation.Registry.Should().Be(FirebirdContainerImageTags.Registry);
	}

	[Fact]
	public async Task GeneratesConnectionString()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();
		IResourceBuilder<FbServerResource> firebird = appBuilder
			.AddFirebird("firebird")
			.WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 3050));

		FbServerResource connectionStringResource = firebird.Resource;
		string? connectionString = await connectionStringResource
			.GetConnectionStringAsync(TestContext.Current.CancellationToken);

		connectionStringResource.ConnectionStringExpression.ValueExpression
			.Should()
			.Be("Host={firebird.bindings.tcp.host};Port={firebird.bindings.tcp.port};Username=SYSDBA;Password={firebird-password.value}");
		connectionString.Should().Be($"Host=localhost;Port=3050;Username=SYSDBA;Password={firebird.Resource.PasswordParameter.Value}");
	}

	[Fact]
	public async Task GeneratesConnectionStringWithDatabase()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder.AddFirebird("firebird")
			.WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 3050))
			.AddDatabase("testDb");

		using DistributedApplication app = appBuilder.Build();

		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? fbResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		fbResource.Should().NotBeNull();
		string? fbConnectionString = await fbResource.GetConnectionStringAsync(TestContext.Current.CancellationToken);
		fbConnectionString.Should().NotBeNull();
		FbDatabaseResource? fbDbResource = appModel.Resources.OfType<FbDatabaseResource>().SingleOrDefault();
		fbDbResource.Should().NotBeNull();
		IResourceWithConnectionString fbDatabaseConnectionStringResource = fbDbResource;
		string? dbConnectionString = await fbDatabaseConnectionStringResource.GetConnectionStringAsync(TestContext.Current.CancellationToken);

		fbDbResource.ConnectionStringExpression.ValueExpression
			.Should()
			.Be("{firebird.connectionString};Database=testDb");
		dbConnectionString.Should().Be($"{fbConnectionString};Database=testDb");
	}

	[Fact]
	public void ThrowsWhenAddingIdenticalDatabases()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		IResourceBuilder<FbServerResource> firebird = appBuilder.AddFirebird("firebird");
		firebird.AddDatabase("db");

		Action act = () => firebird.AddDatabase("db");
		act.Should().ThrowExactly<DistributedApplicationException>();
	}

	[Fact]
	public void ThrowsWhenAddingIdenticalDatabasesToDifferentServers()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder.AddFirebird("firebird1")
			.AddDatabase("db");

		IResourceBuilder<FbServerResource> db = appBuilder.AddFirebird("firebird2");
		Action act = () => db.AddDatabase("db");
		act.Should().ThrowExactly<DistributedApplicationException>();
	}

	[Fact]
	public void AddsDatabasesWithDifferentNamesToSingleServer()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		IResourceBuilder<FbServerResource> firebird = appBuilder.AddFirebird("firebird");

		IResourceBuilder<FbDatabaseResource> db1 = firebird.AddDatabase("db1", "customers1");
		IResourceBuilder<FbDatabaseResource> db2 = firebird.AddDatabase("db2", "customers2");

		db1.Resource.DatabaseName.Should().Be("customers1");
		db2.Resource.DatabaseName.Should().Be("customers2");

		db1.Resource.ConnectionStringExpression.ValueExpression
			.Should().Be("{firebird.connectionString};Database=customers1");
		db2.Resource.ConnectionStringExpression.ValueExpression
			.Should().Be("{firebird.connectionString};Database=customers2");
	}

	[Fact]
	public void AddsSameDatabasesWithDifferentNamesToMultipleServers()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		IResourceBuilder<FbDatabaseResource> db1 = appBuilder.AddFirebird("firebird1")
			.AddDatabase("db1", "imports");

		IResourceBuilder<FbDatabaseResource> db2 = appBuilder.AddFirebird("firebird2")
			.AddDatabase("db2", "imports");

		db1.Resource.DatabaseName.Should().Be("imports");
		db2.Resource.DatabaseName.Should().Be("imports");

		db1.Resource.ConnectionStringExpression.ValueExpression.Should().Be("{firebird1.connectionString};Database=imports");
		db2.Resource.ConnectionStringExpression.ValueExpression.Should().Be("{firebird2.connectionString};Database=imports");
	}

	[Fact]
	public async Task WithUserAddsEnvironmentVariable()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder
			.AddFirebird("firebird")
			.WithUser("Bob");

		using DistributedApplication app = appBuilder.Build();
		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? firebirdResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		firebirdResource.Should().NotBeNull();
		Dictionary<string, string> envVars = await firebirdResource.GetEnvironmentVariableValuesAsync();

		envVars.Should().ContainKey("FIREBIRD_USER");
		envVars["FIREBIRD_USER"].Should().Be("Bob");
	}

	[Fact]
	public async Task WithPasswordAddsEnvironmentVariable()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder.AddFirebird("firebird")
			.WithPassword("secret");

		using DistributedApplication app = appBuilder.Build();
		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? firebirdResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		firebirdResource.Should().NotBeNull();
		Dictionary<string, string> envVars = await firebirdResource.GetEnvironmentVariableValuesAsync();

		envVars.Should().ContainKey("FIREBIRD_PASSWORD");
		envVars["FIREBIRD_PASSWORD"].Should().Be("secret");
	}

	[Fact]
	public async Task WithRootPasswordAddsEnvironmentVariable()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder
			.AddFirebird("firebird")
			.WithRootPassword("very_secret");

		using DistributedApplication app = appBuilder.Build();
		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? firebirdResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		firebirdResource.Should().NotBeNull();
		Dictionary<string, string> envVars = await firebirdResource.GetEnvironmentVariableValuesAsync();

		envVars.Should().ContainKey("FIREBIRD_ROOT_PASSWORD");
		envVars["FIREBIRD_ROOT_PASSWORD"].Should().Be("very_secret");
	}

	[Fact]
	public async Task WithTimeZoneAddsEnvironmentVariable()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder
			.AddFirebird("firebird")
			.WithTimeZone("Europe/Berlin");

		using DistributedApplication app = appBuilder.Build();
		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? firebirdResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		firebirdResource.Should().NotBeNull();
		Dictionary<string, string> envVars = await firebirdResource.GetEnvironmentVariableValuesAsync();

		envVars.Should().ContainKey("TZ");
		envVars["TZ"].Should().Be("Europe/Berlin");
	}

	[Fact]
	public async Task UseLegacyAuthAddsEnvironmentVariable()
	{
		IDistributedApplicationBuilder appBuilder = DistributedApplication.CreateBuilder();

		appBuilder
			.AddFirebird("firebird")
			.UseLegacyAuth();

		using DistributedApplication app = appBuilder.Build();
		DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

		FbServerResource? firebirdResource = appModel.Resources.OfType<FbServerResource>().SingleOrDefault();
		firebirdResource.Should().NotBeNull();
		Dictionary<string, string> envVars = await firebirdResource.GetEnvironmentVariableValuesAsync();

		envVars.Should().ContainKey("FIREBIRD_USE_LEGACY_AUTH");
		envVars["FIREBIRD_USE_LEGACY_AUTH"].Should().Be("true");
	}
}
