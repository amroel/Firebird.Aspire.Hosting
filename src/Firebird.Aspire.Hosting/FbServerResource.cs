using Aspire.Hosting.ApplicationModel;

namespace Firebird.Aspire.Hosting;
public sealed class FbServerResource : ContainerResource, IResourceWithConnectionString
{
	internal const string PrimaryEndpointName = "tcp";
	private const string DefaultUserName = "SYSDBA";
	private readonly Dictionary<string, string> _databases = new(StringComparer.OrdinalIgnoreCase);

	public FbServerResource(string name, ParameterResource? user, ParameterResource password) : base(name)
	{
		UserNameParameter = user;
		PasswordParameter = password;
		PrimaryEndpoint = new(this, PrimaryEndpointName);
	}

	private ReferenceExpression ConnectionString =>
		ReferenceExpression.Create(
			$"Host={PrimaryEndpoint.Property(EndpointProperty.Host)};Port={PrimaryEndpoint.Property(EndpointProperty.Port)};Username={UserNameReference};Password={PasswordParameter}");
	internal ReferenceExpression UserNameReference =>
		UserNameParameter is not null ?
			ReferenceExpression.Create($"{UserNameParameter}") :
			ReferenceExpression.Create($"{DefaultUserName}");

	public EndpointReference PrimaryEndpoint { get; }

	public ParameterResource? UserNameParameter { get; set; }

	public ParameterResource PasswordParameter { get; }

	public ReferenceExpression ConnectionStringExpression
		=> this.TryGetLastAnnotation(out ConnectionStringRedirectAnnotation? annotation)
		? annotation.Resource.ConnectionStringExpression
		: ConnectionString;

	public void AddDatabase(string name, string databaseName) => _databases.TryAdd(name, databaseName);

	public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
	{
		if (this.TryGetLastAnnotation(out ConnectionStringRedirectAnnotation? connectionStringAnnotation))
		{
			return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
		}

		return ConnectionStringExpression.GetValueAsync(cancellationToken);
	}
}
