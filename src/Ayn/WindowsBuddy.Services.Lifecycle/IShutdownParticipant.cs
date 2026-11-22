using System.Threading;
using System.Threading.Tasks;

namespace WindowsBuddy.Services.Lifecycle;

public interface IShutdownParticipant
{
	string Id { get; }

	PersistenceClass Classification { get; }

	OperationGate Operations { get; }

	Task PrepareAsync(CancellationToken cancellation);

	Task QuiesceAsync(CancellationToken cancellation);

	Task VerifyAsync(CancellationToken cancellation);

	Task ResumeAsync();

	Task DisposeAsync();
}
