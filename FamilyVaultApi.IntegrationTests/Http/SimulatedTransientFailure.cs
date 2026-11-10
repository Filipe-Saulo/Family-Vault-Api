using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Erro "transitório" de mentira: o SQLite não tem a execution strategy com retry do MySQL
    public sealed class SimulatedTransientException : Exception
    {
        public SimulatedTransientException() : base("Falha transitória simulada.") { }
    }

    // Faz o papel do EnableRetryOnFailure do MySQL: repete a operação quando a falha é a simulada
    public sealed class RetryOnSimulatedFailureStrategy : ExecutionStrategy
    {
        public RetryOnSimulatedFailureStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero) { }

        protected override bool ShouldRetryOn(Exception exception) => exception is SimulatedTransientException;

        protected override TimeSpan? GetNextDelay(Exception lastException) =>
            base.GetNextDelay(lastException) == null ? null : TimeSpan.Zero;
    }

    // Derruba o primeiro SaveChanges que grava uma despesa (Transaction Added), antes de ir ao banco
    public sealed class FailFirstTransactionSaveInterceptor : SaveChangesInterceptor
    {
        private int _failures;

        public int Failures => _failures;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var addsTransaction = eventData.Context!.ChangeTracker.Entries<Transaction>().Any(e => e.State == EntityState.Added);

            if (addsTransaction && Interlocked.CompareExchange(ref _failures, 1, 0) == 0)
                throw new SimulatedTransientException();

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
