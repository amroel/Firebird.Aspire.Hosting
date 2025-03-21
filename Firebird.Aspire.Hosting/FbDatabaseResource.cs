using Aspire.Hosting.ApplicationModel;

namespace Firebird.Aspire.Hosting;
public sealed class FbDatabaseResource(string name, string dbName, FbServerResource parentResource)
	: Resource(name), IResourceWithParent<FbServerResource>, IResourceWithConnectionString
{
	public FbServerResource Parent { get; } = parentResource;

	public string DatabaseName { get; } = dbName;

	public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{Parent};Database={DatabaseName}");
}
