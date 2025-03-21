using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace Firebird.Aspire.Hosting.Tests;

public class AdditionTests
{
	[Fact]
	public void AddsGeneratedPasswordParameterWithUserSecretsParameterDefaultInRunMode()
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
}
