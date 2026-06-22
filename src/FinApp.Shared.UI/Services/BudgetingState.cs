using FinApp.Contracts;
using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Funds;
using FinApp.Domain.Periods;
using FinApp.Domain.Savings;
using FinApp.Domain.Services;

namespace FinApp.Shared.UI.Services;

/// <summary>
/// Application state the Blazor UI binds to, now backed by the sync server. Holds the signed-in user's
/// account summaries, the loaded full aggregate for the selected account, and the period being viewed.
/// The UI mutates the loaded aggregate through domain methods; every mutation re-serializes the account
/// and pushes the snapshot to the server (which relays the change to other contributors).
/// </summary>
public sealed class BudgetingState(FinAppApiClient api, AuthState auth, SyncClient sync)
{
    private readonly BudgetCoverageService _coverage = new();
    private readonly SavingsReportService _savings = new();

    private List<AccountSummaryDto> _summaries = [];
    private Account? _account;
    private long _version;
    private int _accountIndex;
    private int _selectedIndex;
    private bool _syncStarted;
    private List<InvitationDto> _pendingInvitations = [];

    public bool IsReady { get; private set; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (IsReady || !auth.IsAuthenticated) return;

        if (!_syncStarted)
        {
            sync.AccountChanged += OnAccountChanged;
            sync.InvitationReceived += OnInvitationReceived;
            try { await sync.StartAsync(); } catch { /* live sync is best-effort; REST still works */ }
            _syncStarted = true;
        }

        _summaries = await api.GetAccountsAsync();
        _accountIndex = 0;
        await LoadSelectedAccountAsync();
        await RefreshInvitationsAsync();

        IsReady = true;
        Changed?.Invoke();
    }

    /// <summary>Clear all session state on sign-out.</summary>
    public async Task ResetAsync()
    {
        IsReady = false;
        _summaries = [];
        _account = null;
        _version = 0;
        _accountIndex = 0;
        _selectedIndex = 0;
        _pendingInvitations = [];
        _syncStarted = false;
        sync.AccountChanged -= OnAccountChanged;
        sync.InvitationReceived -= OnInvitationReceived;
        await sync.StopAsync();
        Changed?.Invoke();
    }

    // --- Accounts ---------------------------------------------------------

    public bool HasAccounts => _summaries.Count > 0;
    public Account Account => _account!;
    public IReadOnlyList<AccountSummaryDto> Accounts => _summaries;
    public Guid CurrentAccountId => _account?.Id ?? Guid.Empty;

    /// <summary>True when the signed-in user owns the current account (gates rename/delete).</summary>
    public bool IsOwnerOfCurrent => _account is not null && _account.IsOwner(auth.UserId);

    public async Task SwitchAccount(Guid accountId)
    {
        var index = _summaries.FindIndex(a => a.Id == accountId);
        if (index < 0 || index == _accountIndex) return;
        _accountIndex = index;
        await LoadSelectedAccountAsync();
        Changed?.Invoke();
    }

    public async Task AddAccount(string name, string currency)
    {
        if (_summaries.Any(a => NameEquals(a.Name, name)))
            throw new InvalidOperationException($"You already have an account named “{name.Trim()}”.");
        var summary = await api.CreateAccountAsync(new CreateAccountRequest(name, currency));
        _summaries.Add(summary);
        _accountIndex = _summaries.Count - 1;
        await LoadSelectedAccountAsync(); // empty snapshot -> seeds the starter body and saves
        Changed?.Invoke();
    }

    public async Task RenameAccount(string name)
    {
        var id = CurrentAccountId;
        if (_summaries.Any(a => a.Id != id && NameEquals(a.Name, name)))
            throw new InvalidOperationException($"You already have an account named “{name.Trim()}”.");
        await api.RenameAccountAsync(id, name);
        _account!.Rename(name);
        _summaries[_accountIndex] = _summaries[_accountIndex] with { Name = name };
        Changed?.Invoke();
    }

    public async Task RemoveAccount(Guid accountId)
    {
        await api.DeleteAccountAsync(accountId);
        var index = _summaries.FindIndex(a => a.Id == accountId);
        if (index >= 0) _summaries.RemoveAt(index);
        if (_accountIndex >= _summaries.Count)
            _accountIndex = Math.Max(0, _summaries.Count - 1);
        await LoadSelectedAccountAsync();
        Changed?.Invoke();
    }

    private async Task LoadSelectedAccountAsync()
    {
        if (_summaries.Count == 0) { _account = null; _version = 0; return; }

        var summary = _summaries[_accountIndex];
        var snapshot = await api.GetSnapshotAsync(summary.Id);
        _version = snapshot.Version;

        if (string.IsNullOrEmpty(snapshot.Payload))
        {
            // Brand-new account: build from the header, seed the starter body, and save v1.
            _account = AccountSnapshotSerializer.CreateForHeader(
                summary.Id, summary.Name, summary.Currency, summary.OwnerUserId,
                summary.Members.Select(m => (m.UserId, m.DisplayName)));
            SeedStarterBody(_account);
            await PushSnapshotAsync();
        }
        else
        {
            _account = AccountSnapshotSerializer.Deserialize(snapshot.Payload);
            ReconcileHeader(_account, summary);
        }

        _selectedIndex = _account.Periods.Count - 1;
        await sync.SubscribeAsync(summary.Id);
    }

    /// <summary>Ensure the loaded aggregate reflects server-authoritative header data (name + members).</summary>
    private static void ReconcileHeader(Account account, AccountSummaryDto summary)
    {
        if (account.Name != summary.Name) account.Rename(summary.Name);
        foreach (var m in summary.Members)
            if (!account.IsContributor(m.UserId))
                account.AddMember(m.UserId, m.DisplayName);
    }

    /// <summary>Serialize the current aggregate and push it to the server, advancing the version.</summary>
    private async Task PushSnapshotAsync()
    {
        var payload = AccountSnapshotSerializer.Serialize(_account!);
        var saved = await api.SaveSnapshotAsync(_account!.Id, new SaveAccountRequest(payload, _version));
        _version = saved.Version;
    }

    // --- Period navigation ------------------------------------------------

    public Period Period => Account.Periods[_selectedIndex];
    public int PeriodNumber => _selectedIndex + 1;
    public int PeriodCount => Account.Periods.Count;
    public bool CanGoPrev => _selectedIndex > 0;
    public bool CanGoNext => _selectedIndex < Account.Periods.Count - 1;
    public bool IsLatestPeriod => _selectedIndex == Account.Periods.Count - 1;

    public void GoPrev() { if (CanGoPrev) { _selectedIndex--; Changed?.Invoke(); } }
    public void GoNext() { if (CanGoNext) { _selectedIndex++; Changed?.Invoke(); } }

    public string Currency => Account.Currency;
    public Money Money(decimal amount) => new(amount, Currency);

    // --- Funds ------------------------------------------------------------

    public IReadOnlyList<Fund> Funds => Account.Funds;
    /// <summary>Top-level funds — the ones that actually hold money (sub-funds are informational labels only).</summary>
    public IReadOnlyList<Fund> RootFunds => Account.RootFunds.ToList();
    public IReadOnlyList<Fund> ChildFundsOf(Guid parentId) => Account.ChildFundsOf(parentId).ToList();
    public Fund? FindFund(Guid fundId) => Account.FindFund(fundId);
    public string FundName(Guid fundId) => Account.FundName(fundId);
    public Money FundBalance(Guid fundId) => Period.FundBalance(fundId);
    public Money FundOpeningBalance(Guid fundId) =>
        Period.InitialBalances.FirstOrDefault(b => b.FundId == fundId)?.Amount ?? Money(0);
    public string? FundRemovalBlocker(Guid fundId) => Account.FundRemovalBlocker(fundId);

    public IReadOnlyList<FundTransfer> FundTransfers =>
        Period.FundTransfers.OrderByDescending(t => t.Date).ToList();

    private Guid DefaultFundId => Account.RootFunds.FirstOrDefault()?.Id ?? Guid.Empty;

    /// <summary>The period's opening balance: the sum of the real (non-informative) initial fund values.
    /// Independent of how the money is later budgeted/saved (unallocations never change it).</summary>
    public Money OpeningBalance => Period.InitialTotal;

    /// <summary>Physical money expected to carry into the next period.</summary>
    public Money ClosingBalance => Period.ExpectedClosingBalance;

    /// <summary>This period's transfers sent out to other accounts (newest first).</summary>
    public IReadOnlyList<ExternalTransfer> ExternalTransfers =>
        Period.ExternalTransfers.OrderByDescending(t => t.Date).ToList();

    // --- Category tree & budgets (reads) ----------------------------------

    public IEnumerable<Category> RootCategories => Account.RootCategories;
    public IEnumerable<Category> ChildrenOf(Guid parentId) => Account.ChildrenOfCategory(parentId);
    public IReadOnlyList<Category> AllCategories => Account.Categories;

    /// <summary>Categories in tree order with their depth, for an indented &lt;select&gt; (parents above their children).</summary>
    public IReadOnlyList<(Category Category, int Depth)> CategoryOptions
    {
        get
        {
            var result = new List<(Category, int)>();
            void Walk(IEnumerable<Category> nodes, int depth)
            {
                foreach (var c in nodes)
                {
                    result.Add((c, depth));
                    Walk(Account.ChildrenOfCategory(c.Id), depth + 1);
                }
            }
            Walk(Account.RootCategories, 0);
            return result;
        }
    }
    public Budget? BudgetFor(Guid categoryId) => Period.FindBudget(categoryId);
    public bool HasBudget(Guid categoryId) => Period.FindBudget(categoryId) is not null;
    public BudgetCoverage Coverage(Guid categoryId) => _coverage.ForCategory(Account, Period, categoryId);
    public Money Leftover(Guid categoryId) => Coverage(categoryId).Remaining;
    public string? CategoryRemovalBlocker(Guid categoryId) => Account.CategoryRemovalBlocker(categoryId);
    public string CategoryName(Guid categoryId) => Account.FindCategory(categoryId)?.Name ?? "—";
    public string? ParentName(Guid? parentId) => parentId is { } p ? Account.FindCategory(p)?.Name : null;

    public IEnumerable<Category> BudgetedCategories =>
        Period.Budgets.Select(b => Account.FindCategory(b.CategoryId)!).Where(c => c is not null);

    // --- Totals & reports -------------------------------------------------

    public Money TotalBudgeted => Period.BudgetedTotal;
    public Money TotalSpent => Period.ExpensesTotal;

    /// <summary>The contributed pool you can allocate from: new member deposits plus the "From previous period"
    /// leftover (positive carried-in, or a negative shortfall until it's covered).</summary>
    public Money TotalContributed => Period.ContributionsPaidTotal + Period.CarriedIn;

    /// <summary>Shortfall still to cover (positive), or zero — already net of member deposits.</summary>
    public Money UnallocatedShortfall => Period.UnallocatedShortfall;
    public bool HasUnallocatedShortfall => Period.UnallocatedShortfall.Amount > 0m;

    /// <summary>The pseudo-category id used as the "From previous period" spend-savings destination.</summary>
    public static Guid CarryoverCategoryId => Period.CarryoverSource;

    /// <summary>Cover the carried shortfall by spending from a savings bucket (a "From previous period" movement).</summary>
    public Task CoverCarryoverFromSavings(Guid savingCategoryId, decimal amount)
    {
        Period.CoverCarryoverFromSavings(savingCategoryId, Money(amount), Today());
        return SaveAsync();
    }

    /// <summary>Savings earmarked beyond actual cash left — overspend to reconcile next period.</summary>
    public Money Deficit => Period.Deficit;
    public Money SavingsThisPeriod => Period.SavingsNetTotal;
    public Money SavingsAccumulated => _savings.AccumulatedTotal(Account);
    public Money MaxAdditionalSavings => Period.MaxAdditionalSavings;
    public Money AvailableToSave => Period.AvailableToSave;

    /// <summary>The "From previous period" carryover contribution for this period (0 if none).</summary>
    public Money CarryoverThisPeriod => Period.CarryoverTotal;

    public IReadOnlyList<Expense> RecentExpenses =>
        Period.Expenses.OrderByDescending(e => e.Date).Take(10).ToList();

    public IReadOnlyList<Expense> AllExpenses =>
        Period.Expenses.OrderByDescending(e => e.Date).ToList();

    public IReadOnlyList<Expense> ExpensesFor(Guid categoryId) =>
        Period.Expenses.Where(e => e.CategoryId == categoryId).OrderByDescending(e => e.Date).ToList();

    public Money Unallocated => MaxAdditionalSavings;
    public bool HasUnallocatedFunds => Unallocated.Amount > 0m;
    public bool IsPeriodOpen => Period.Status == PeriodStatus.Open;

    public Expense? FindExpense(Guid id) => Period.Expenses.FirstOrDefault(e => e.Id == id);

    public decimal? PeriodSavingsRate => _savings.PeriodSavingsRate(Period);
    public decimal? AccountSavingsRate => _savings.AccountSavingsRate(Account);

    public SavingCategory? FindSavingBucket(Guid id) => Account.FindSavingCategory(id);
    public SavingGoalProgress SavingGoal(Guid bucketId) => _savings.GoalProgress(Account, bucketId);
    public string? SavingBucketRemovalBlocker(Guid id) => Account.SavingCategoryRemovalBlocker(id);

    public string MemberName(Guid memberId) =>
        Account.Members.FirstOrDefault(m => m.UserId == memberId)?.DisplayName ?? "—";

    public IReadOnlyList<(SavingCategory Bucket, Money Total)> SavingBuckets =>
        Account.SavingCategories
            .Select(b => (b, _savings.ForBucket(Account, Period, b.Id).AccumulatedTotal))
            .ToList();

    public string SavingBucketName(Guid id) => FindSavingBucket(id)?.Name ?? "—";

    /// <summary>This period's manual "Add to savings" deposits, newest first (editable/removable).</summary>
    public IReadOnlyList<SavingAllocation> SavingDepositsThisPeriod =>
        Period.ManualSavingDeposits().OrderByDescending(a => a.Date).ToList();

    public SavingAllocation? FindSavingDeposit(Guid id) =>
        Period.ManualSavingDeposits().FirstOrDefault(a => a.Id == id);

    /// <summary>This period's savings spendings (money matured into a budget, or moved between buckets), newest first.</summary>
    public IReadOnlyList<SavingAllocation> SavingMovementsThisPeriod =>
        Period.SavingMovements().OrderByDescending(a => a.Date).ToList();

    public SavingAllocation? FindSavingMovement(Guid id) =>
        Period.SavingMovements().FirstOrDefault(a => a.Id == id);

    /// <summary>A human-readable destination for a savings movement row (a budget category, or another bucket).</summary>
    public string SavingMovementTarget(SavingAllocation movement)
    {
        if (movement.BudgetCategoryId is { } categoryId)
            return categoryId == Period.CarryoverSource
                ? $"{SavingBucketName(movement.SavingCategoryId)} → From previous period"
                : $"{SavingBucketName(movement.SavingCategoryId)} → {CategoryName(categoryId)} (budget)";
        if (movement.TransferPairId is { } pairId)
        {
            var toId = Period.SavingAllocations
                .Where(a => a.TransferPairId == pairId && !a.Amount.IsNegative)
                .Select(a => a.SavingCategoryId)
                .FirstOrDefault();
            return $"{SavingBucketName(movement.SavingCategoryId)} → {SavingBucketName(toId)} (bucket)";
        }
        return SavingBucketName(movement.SavingCategoryId);
    }

    public Task EditSavingMovement(Guid allocationId, decimal amount)
    {
        Period.EditSavingMovement(allocationId, Money(amount));
        return SaveAsync();
    }

    public Task RemoveSavingMovement(Guid allocationId)
    {
        Period.RemoveSavingMovement(allocationId);
        return SaveAsync();
    }

    public IReadOnlyList<AccountMember> Members => Account.Members;
    public Contribution? ContributionFor(Guid memberId) =>
        Period.Contributions.FirstOrDefault(c => c.MemberId == memberId);

    /// <summary>Who the current actions are attributed to — the signed-in user (a member of the account).</summary>
    private Guid CurrentMemberId => auth.UserId;

    // --- Commands ---------------------------------------------------------

    public Task AddExpense(Guid categoryId, decimal amount, Guid fundId, string? note, DateOnly date)
    {
        Period.AddExpense(new Expense(categoryId, Money(amount), date, CurrentMemberId, fundId, note));
        return SaveAsync();
    }

    public Task EditExpense(Guid expenseId, Guid categoryId, decimal amount, Guid fundId, string? note, DateOnly date)
    {
        Period.EditExpense(expenseId, categoryId, Money(amount), fundId, note, date);
        return SaveAsync();
    }

    public Task RemoveExpense(Guid expenseId)
    {
        Period.RemoveExpense(expenseId);
        return SaveAsync();
    }

    public Task RecordDeposit(Guid memberId, decimal amount)
    {
        Period.Deposit(memberId, Money(amount));
        return SaveAsync();
    }

    /// <summary>Overwrite a member's deposited total.</summary>
    public Task EditDeposit(Guid memberId, decimal amount)
    {
        Period.SetDeposit(memberId, Money(amount));
        return SaveAsync();
    }

    /// <summary>Clear a member's deposit.</summary>
    public Task RemoveDeposit(Guid memberId)
    {
        Period.RemoveDeposit(memberId);
        return SaveAsync();
    }

    public Task AllocateSaving(Guid savingCategoryId, decimal amount, string? note)
    {
        Period.AllocateToSavings(savingCategoryId, Money(amount), Today(), note);
        return SaveAsync();
    }

    public Task EditSavingDeposit(Guid allocationId, decimal amount)
    {
        Period.EditSavingDeposit(allocationId, Money(amount));
        return SaveAsync();
    }

    public Task RemoveSavingDeposit(Guid allocationId)
    {
        Period.RemoveSavingAllocation(allocationId);
        return SaveAsync();
    }

    public Task SpendFromSavings(Guid savingCategoryId, Guid categoryId, decimal amount, string? note)
    {
        Period.ConvertSavingToExpense(savingCategoryId, categoryId, Money(amount), Today(),
            CurrentMemberId, DefaultFundId, note);
        return SaveAsync();
    }

    public Task ConvertSavingToBudget(Guid savingCategoryId, Guid categoryId, decimal amount, string? note)
    {
        Period.ConvertSavingToBudget(savingCategoryId, categoryId, Money(amount), Today(), note);
        return SaveAsync();
    }

    /// <summary>Move earmarked money from one savings bucket to another (net-neutral).</summary>
    public Task MoveSavingToBucket(Guid fromBucketId, Guid toBucketId, decimal amount, string? note)
    {
        Period.TransferSavings(fromBucketId, toBucketId, Money(amount), Today(), note);
        return SaveAsync();
    }

    /// <summary>True during initial setup (only the first period exists) — when a bucket's pre-existing initial balance may be set.</summary>
    public bool CanSetInitialSavings => PeriodCount == 1;

    // Saving bucket CRUD
    public async Task<Guid> AddSavingBucket(string name, decimal? goalAmount, decimal thresholdPercent, bool notifyOnMilestone, decimal initialAmount)
    {
        var bucket = Account.AddSavingCategory(name);
        if (goalAmount is > 0m)
            Account.ConfigureSavingGoal(bucket.Id, goalAmount, thresholdPercent / 100m, notifyOnMilestone);
        if (CanSetInitialSavings && initialAmount > 0m)
            Account.SetSavingInitialAmount(bucket.Id, initialAmount);
        await SaveAsync();
        return bucket.Id;
    }

    public Task SaveSavingBucket(Guid savingCategoryId, string name, decimal? goalAmount, decimal thresholdPercent, bool notifyOnMilestone, decimal initialAmount)
    {
        Account.RenameSavingCategory(savingCategoryId, name);
        Account.ConfigureSavingGoal(savingCategoryId, goalAmount is > 0m ? goalAmount : null, thresholdPercent / 100m, notifyOnMilestone);
        if (CanSetInitialSavings)
            Account.SetSavingInitialAmount(savingCategoryId, initialAmount);
        return SaveAsync();
    }

    public decimal SavingInitialAmount(Guid savingCategoryId) => FindSavingBucket(savingCategoryId)?.InitialAmount ?? 0m;

    public Task RemoveSavingBucket(Guid savingCategoryId)
    {
        Account.RemoveSavingCategory(savingCategoryId);
        return SaveAsync();
    }

    // Fund CRUD + transfers
    public async Task<Guid> AddFund(string name, Guid? parentId = null)
    {
        var fund = Account.AddFund(name, parentId);
        await SaveAsync();
        return fund.Id;
    }

    public Task RenameFund(Guid fundId, string name)
    {
        Account.RenameFund(fundId, name);
        return SaveAsync();
    }

    public bool FundHasOpeningBalance(Guid fundId) => Account.FundHasOpeningBalance(fundId);

    public Task RemoveFund(Guid fundId, Guid? moveOpeningBalancesTo = null)
    {
        Account.RemoveFund(fundId, moveOpeningBalancesTo);
        return SaveAsync();
    }

    public Task SetFundOpeningBalance(Guid fundId, decimal amount)
    {
        // A sub-fund's opening balance is informational only (excluded from the real total).
        var informative = Account.FindFund(fundId)?.IsRoot == false;
        Period.SetInitialBalance(fundId, Money(amount), informative);
        return SaveAsync();
    }

    /// <summary>Sum of a parent fund's sub-fund (informative) opening balances this period.</summary>
    public Money SubFundOpeningTotal(Guid parentId) =>
        ChildFundsOf(parentId).Aggregate(Money(0), (sum, f) => sum + FundOpeningBalance(f.Id));

    /// <summary>True when a parent's sub-funds don't add up to its opening balance (drives a soft hint; never blocks).</summary>
    public bool SubFundsMismatch(Guid parentId) =>
        ChildFundsOf(parentId).Count > 0 && SubFundOpeningTotal(parentId).Amount != FundOpeningBalance(parentId).Amount;

    public Task TransferFunds(Guid fromFundId, Guid toFundId, decimal amount, string? note)
    {
        Period.TransferFunds(fromFundId, toFundId, Money(amount), Today(), note);
        return SaveAsync();
    }

    public FundTransfer? FindFundTransfer(Guid id) => Period.FundTransfers.FirstOrDefault(t => t.Id == id);

    public Task EditFundTransfer(Guid id, Guid fromFundId, Guid toFundId, decimal amount, string? note)
    {
        Period.EditFundTransfer(id, fromFundId, toFundId, Money(amount), note);
        return SaveAsync();
    }

    public Task RemoveFundTransfer(Guid id)
    {
        Period.RemoveFundTransfer(id);
        return SaveAsync();
    }

    // --- Cross-account transfers (money out -> a contribution in another account) ---

    /// <summary>Other accounts the money could be sent to: the user's other accounts in the same currency.</summary>
    public IReadOnlyList<AccountSummaryDto> TransferableAccounts =>
        _summaries.Where(a => a.Id != CurrentAccountId && a.Currency == Currency).ToList();

    public string AccountName(Guid accountId) =>
        _summaries.FirstOrDefault(a => a.Id == accountId)?.Name ?? "another account";

    public ExternalTransfer? FindExternalTransfer(Guid id) =>
        Period.ExternalTransfers.FirstOrDefault(t => t.Id == id);

    /// <summary>
    /// Send money from one of this account's funds to another account. The source records a real outflow
    /// (lowering the fund and the closing balance); the destination's current period receives it as a deposit
    /// from the signed-in user. Two snapshots are pushed — this account's, then the destination's.
    /// </summary>
    public async Task TransferToAccount(Guid destinationAccountId, Guid fromFundId, decimal amount, string? note)
    {
        if (amount <= 0m) return;
        var destination = _summaries.FirstOrDefault(a => a.Id == destinationAccountId)
            ?? throw new InvalidOperationException("Destination account not found.");
        if (destination.Currency != Currency)
            throw new InvalidOperationException("Both accounts must use the same currency.");

        // 1) Record the outflow on this account and push it.
        Period.TransferOut(fromFundId, Money(amount), Today(), destinationAccountId, note);
        await SaveAsync();

        // 2) Load the destination, deposit into its current period for the signed-in user, and push it.
        var snapshot = await api.GetSnapshotAsync(destinationAccountId);
        if (string.IsNullOrEmpty(snapshot.Payload))
            throw new InvalidOperationException($"Open “{destination.Name}” once before transferring into it.");
        var destAccount = AccountSnapshotSerializer.Deserialize(snapshot.Payload);
        var destPeriod = destAccount.CurrentPeriod
            ?? throw new InvalidOperationException($"“{destination.Name}” has no open period to receive the transfer.");
        destPeriod.Deposit(auth.UserId, new Money(amount, destAccount.Currency));
        var payload = AccountSnapshotSerializer.Serialize(destAccount);
        await api.SaveSnapshotAsync(destinationAccountId, new SaveAccountRequest(payload, snapshot.Version));
    }

    public Task RemoveExternalTransfer(Guid id)
    {
        Period.RemoveExternalTransfer(id);
        return SaveAsync();
    }

    public Task ReschedulePeriod(DateOnly from, DateOnly to)
    {
        Account.ReschedulePeriod(Period, from, to);
        return SaveAsync();
    }

    // Category CRUD
    public async Task<Guid> AddCategory(string name, Guid? parentId)
    {
        var category = Account.AddCategory(name, parentId);
        await SaveAsync();
        return category.Id;
    }

    public Task RenameCategory(Guid categoryId, string name)
    {
        Account.RenameCategory(categoryId, name);
        return SaveAsync();
    }

    public Task RemoveCategory(Guid categoryId)
    {
        Account.RemoveCategory(categoryId);
        return SaveAsync();
    }

    // Budget CRUD
    public Task SaveBudget(Guid categoryId, decimal amount, decimal thresholdPercent, bool notifyEvery)
    {
        Period.SetBudget(categoryId, Money(amount), thresholdPercent / 100m, notifyEvery);
        return SaveAsync();
    }

    public Task RemoveBudget(Guid categoryId)
    {
        Period.RemoveBudget(categoryId);
        return SaveAsync();
    }

    /// <summary>Remove the latest period and make the previous one active again.</summary>
    public Task RemoveLatestPeriod()
    {
        Account.RemoveLatestPeriod();
        _selectedIndex = Account.Periods.Count - 1;
        return SaveAsync();
    }

    /// <summary>
    /// Start the next period. The caller passes each top-level fund's real current balance, which becomes the
    /// new period's opening balance. The "From previous period" contribution is this period's opening total
    /// minus the previous period's closing balance (clamped at zero) — the money carried over for allocation.
    /// It's excluded from the closing balance since it already sits in the openings.
    /// </summary>
    public Task StartNextPeriod(bool copyBudgets, IReadOnlyDictionary<Guid, decimal> realFundOpenings)
    {
        var previous = Account.CurrentPeriod!;
        var prevClosing = previous.ExpectedClosingBalance;
        previous.Close();

        var from = previous.To.AddDays(1);
        var to = from.AddMonths(1).AddDays(-1);
        var next = Account.StartPeriod(from, to, copyBudgets);

        var realTotal = Money(0);
        foreach (var f in Account.RootFunds)
        {
            var amount = Money(realFundOpenings.TryGetValue(f.Id, out var v) ? v : 0m);
            next.SetInitialBalance(f.Id, amount);
            realTotal += amount;
        }

        next.SetCarryover(realTotal - prevClosing); // leftover = this opening − previous closing

        _selectedIndex = Account.Periods.Count - 1;
        return SaveAsync();
    }

    // --- Invitations ------------------------------------------------------

    public IReadOnlyList<InvitationDto> PendingInvitations => _pendingInvitations;
    public int PendingInvitationCount => _pendingInvitations.Count;

    public async Task RefreshInvitationsAsync()
    {
        _pendingInvitations = await api.GetPendingInvitationsAsync();
        Changed?.Invoke();
    }

    public Task InviteToCurrentAccount(string username) => api.InviteAsync(CurrentAccountId, username);

    public async Task AcceptInvitation(Guid invitationId)
    {
        var accountId = await api.AcceptInvitationAsync(invitationId);
        await sync.SubscribeAsync(accountId);
        _summaries = await api.GetAccountsAsync();
        _accountIndex = Math.Max(0, _summaries.FindIndex(a => a.Id == accountId));
        await LoadSelectedAccountAsync();
        await RefreshInvitationsAsync();
        Changed?.Invoke();
    }

    public async Task DeclineInvitation(Guid invitationId)
    {
        await api.DeclineInvitationAsync(invitationId);
        await RefreshInvitationsAsync();
    }

    // --- Live sync handlers (fire on a background thread) ------------------

    private async void OnAccountChanged(AccountChangedEvent e)
    {
        if (_account is null || e.AccountId != _account.Id || e.ChangedByUserId == auth.UserId) return;
        try
        {
            var snapshot = await api.GetSnapshotAsync(e.AccountId);
            if (!string.IsNullOrEmpty(snapshot.Payload))
            {
                _version = snapshot.Version;
                _account = AccountSnapshotSerializer.Deserialize(snapshot.Payload);
                ReconcileHeader(_account, _summaries[_accountIndex]);
                _selectedIndex = Math.Min(_selectedIndex, _account.Periods.Count - 1);
                Changed?.Invoke();
            }
        }
        catch { /* a transient reload failure shouldn't crash the UI */ }
    }

    private async void OnInvitationReceived(InvitationReceivedEvent e)
    {
        try { await RefreshInvitationsAsync(); } catch { /* best effort */ }
    }

    // --- Helpers ----------------------------------------------------------

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    private static bool NameEquals(string existing, string candidate) =>
        string.Equals(existing.Trim(), candidate?.Trim(), StringComparison.OrdinalIgnoreCase);

    private Task SaveAsync()
    {
        Changed?.Invoke();
        return PushSnapshotAsync();
    }

    /// <summary>A fresh, usable account body: starter categories/buckets, default funds, and the current month's period.</summary>
    private static void SeedStarterBody(Account account)
    {
        foreach (var c in new[] { "Food", "Bills", "Transport", "Other" })
            account.AddCategory(c);
        account.AddSavingCategory("General");
        account.AddDefaultFunds();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1);
        account.StartPeriod(from, from.AddMonths(1).AddDays(-1));
    }
}
