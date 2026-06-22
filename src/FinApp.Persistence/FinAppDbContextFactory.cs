using Microsoft.EntityFrameworkCore.Design;

namespace FinApp.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to build the model and emit
/// migrations. It deliberately builds options <b>without</b> a SQLCipher key — migrations describe
/// schema only and never touch a real (encrypted) database, so no key is needed or wanted here.
/// At runtime the app still opens the encrypted file via <see cref="FinAppDb.BuildOptions"/>.
/// </summary>
public sealed class FinAppDbContextFactory : IDesignTimeDbContextFactory<FinAppDbContext>
{
    public FinAppDbContext CreateDbContext(string[] args) =>
        new(FinAppDb.BuildOptions("finapp.design.db", password: null));
}
