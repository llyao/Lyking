using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Commercial
{
    public sealed class StoreProduct
    {
        public StoreProduct(string productId, string title, string localizedPrice, string currencyCode, bool isConsumable)
        {
            ProductId = RequireText(productId, nameof(productId));
            Title = title ?? string.Empty;
            LocalizedPrice = localizedPrice ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
            IsConsumable = isConsumable;
        }

        public string ProductId { get; }
        public string Title { get; }
        public string LocalizedPrice { get; }
        public string CurrencyCode { get; }
        public bool IsConsumable { get; }

        private static string RequireText(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("商品 ID 不能为空。", name) : value;
    }

    public sealed class StoreReceipt
    {
        public StoreReceipt(string transactionId, string productId, string platform, string payload)
        {
            TransactionId = RequireText(transactionId, nameof(transactionId));
            ProductId = RequireText(productId, nameof(productId));
            Platform = RequireText(platform, nameof(platform));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public string TransactionId { get; }
        public string ProductId { get; }
        public string Platform { get; }
        public string Payload { get; }

        private static string RequireText(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", name) : value;
    }

    public enum PurchaseStatus
    {
        Purchased,
        Pending,
        Cancelled,
        Failed,
    }

    public sealed class StorePurchaseResult
    {
        public StorePurchaseResult(PurchaseStatus status, StoreReceipt? receipt = null, string? error = null)
        {
            Status = status;
            Receipt = receipt;
            Error = error;
            if (status == PurchaseStatus.Purchased && receipt == null)
            {
                throw new ArgumentException("已购买结果必须包含收据。", nameof(receipt));
            }
        }

        public PurchaseStatus Status { get; }
        public StoreReceipt? Receipt { get; }
        public string? Error { get; }
    }

    public interface IStoreService
    {
        ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(CancellationToken cancellationToken = default);
        ValueTask<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyList<StoreReceipt>> RestorePurchasesAsync(CancellationToken cancellationToken = default);
        ValueTask<bool> AcknowledgeTransactionAsync(
            StoreReceipt receipt,
            ReceiptVerificationResult verification,
            CancellationToken cancellationToken = default);
    }

    public sealed class SimulationStoreService : IStoreService
    {
        private readonly Dictionary<string, StoreProduct> _products;
        private readonly List<StoreReceipt> _receipts = new List<StoreReceipt>();
        private readonly HashSet<string> _acknowledgedTransactionIds = new HashSet<string>(StringComparer.Ordinal);
        private int _nextTransaction;
        private PurchaseStatus? _nextPurchaseStatus;

        public SimulationStoreService(IEnumerable<StoreProduct> products)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            _products = products.ToDictionary(product => product.ProductId, StringComparer.Ordinal);
        }

        public ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<StoreProduct>>(_products.Values.ToArray());
        }

        public void SetNextPurchaseStatus(PurchaseStatus status)
        {
            if (!Enum.IsDefined(typeof(PurchaseStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            _nextPurchaseStatus = status;
        }

        public IReadOnlyList<string> AcknowledgedTransactionIds
        {
            get
            {
                lock (_receipts)
                {
                    return _acknowledgedTransactionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                }
            }
        }

        public ValueTask<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurchaseStatus status = _nextPurchaseStatus ?? PurchaseStatus.Purchased;
            _nextPurchaseStatus = null;
            if (status != PurchaseStatus.Purchased)
            {
                return new ValueTask<StorePurchaseResult>(new StorePurchaseResult(
                    status,
                    error: status == PurchaseStatus.Failed ? "模拟商店购买失败。" : null));
            }

            if (!_products.ContainsKey(productId))
            {
                return new ValueTask<StorePurchaseResult>(new StorePurchaseResult(PurchaseStatus.Failed, error: "商品不存在。"));
            }

            int transaction = Interlocked.Increment(ref _nextTransaction);
            string transactionId = "simulation-tx-" + transaction.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            var receipt = new StoreReceipt(transactionId, productId, "simulation", "simulation-receipt:" + transactionId + ":" + productId);
            lock (_receipts)
            {
                _receipts.Add(receipt);
            }

            return new ValueTask<StorePurchaseResult>(new StorePurchaseResult(PurchaseStatus.Purchased, receipt));
        }

        public ValueTask<IReadOnlyList<StoreReceipt>> RestorePurchasesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_receipts)
            {
                StoreReceipt[] restorable = _receipts
                    .Where(receipt => !_products[receipt.ProductId].IsConsumable)
                    .ToArray();
                return new ValueTask<IReadOnlyList<StoreReceipt>>(restorable);
            }
        }

        public ValueTask<bool> AcknowledgeTransactionAsync(
            StoreReceipt receipt,
            ReceiptVerificationResult verification,
            CancellationToken cancellationToken = default)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            if (verification == null)
            {
                throw new ArgumentNullException(nameof(verification));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (verification.Status != ReceiptVerificationStatus.Verified
                || !string.Equals(verification.TransactionId, receipt.TransactionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("只有同一交易的可信验证成功结果才能确认平台交易。");
            }

            lock (_receipts)
            {
                if (!_receipts.Any(stored => SameReceipt(stored, receipt)))
                {
                    throw new InvalidOperationException("收据不属于当前模拟商店交易。");
                }

                return new ValueTask<bool>(_acknowledgedTransactionIds.Add(receipt.TransactionId));
            }
        }

        private static bool SameReceipt(StoreReceipt left, StoreReceipt right) =>
            string.Equals(left.TransactionId, right.TransactionId, StringComparison.Ordinal)
            && string.Equals(left.ProductId, right.ProductId, StringComparison.Ordinal)
            && string.Equals(left.Platform, right.Platform, StringComparison.Ordinal)
            && string.Equals(left.Payload, right.Payload, StringComparison.Ordinal);
    }

    public enum ReceiptVerificationStatus
    {
        Verified,
        Rejected,
        Unavailable,
    }

    public sealed class ReceiptVerificationResult : IEquatable<ReceiptVerificationResult>
    {
        internal ReceiptVerificationResult(ReceiptVerificationStatus status, string transactionId, string? reason = null)
        {
            if (!Enum.IsDefined(typeof(ReceiptVerificationStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            TransactionId = transactionId ?? throw new ArgumentNullException(nameof(transactionId));
            Reason = reason;
        }

        public ReceiptVerificationStatus Status { get; }
        public string TransactionId { get; }
        public string? Reason { get; }

        public bool Equals(ReceiptVerificationResult? other) => other != null
            && Status == other.Status
            && string.Equals(TransactionId, other.TransactionId, StringComparison.Ordinal)
            && string.Equals(Reason, other.Reason, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as ReceiptVerificationResult);

        public override int GetHashCode() => (int)Status * 397 ^ TransactionId.GetHashCode();
    }

    /// <summary>验证收据的后端适配合同；验证结果仅供服务端权益流程使用，本客户端不授予权益。</summary>
    public interface IReceiptVerifier
    {
        ValueTask<ReceiptVerificationResult> VerifyAsync(StoreReceipt receipt, CancellationToken cancellationToken = default);
    }

    /// <summary>供应商验证适配基类；仅可信验证器实现应生成成功结果。</summary>
    public abstract class ReceiptVerifierBase : IReceiptVerifier
    {
        public abstract ValueTask<ReceiptVerificationResult> VerifyAsync(StoreReceipt receipt, CancellationToken cancellationToken = default);

        protected static ReceiptVerificationResult CreateResult(
            ReceiptVerificationStatus status,
            string transactionId,
            string? reason = null) => new(status, transactionId, reason);
    }

    public sealed class SimulationReceiptVerifier : ReceiptVerifierBase
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, StoreReceipt> _trustedReceipts = new Dictionary<string, StoreReceipt>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReceiptVerificationResult> _results = new Dictionary<string, ReceiptVerificationResult>(StringComparer.Ordinal);

        public SimulationReceiptVerifier(IEnumerable<StoreReceipt> trustedReceipts)
        {
            if (trustedReceipts == null)
            {
                throw new ArgumentNullException(nameof(trustedReceipts));
            }

            foreach (StoreReceipt receipt in trustedReceipts)
            {
                if (_trustedReceipts.ContainsKey(receipt.TransactionId))
                {
                    throw new ArgumentException("模拟验证器不能包含重复交易标识。", nameof(trustedReceipts));
                }

                _trustedReceipts.Add(receipt.TransactionId, receipt);
            }
        }

        public int UniqueTransactionCount
        {
            get { lock (_gate) { return _results.Count; } }
        }

        public override ValueTask<ReceiptVerificationResult> VerifyAsync(StoreReceipt receipt, CancellationToken cancellationToken = default)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_trustedReceipts.TryGetValue(receipt.TransactionId, out StoreReceipt? expected)
                    && SameReceipt(expected, receipt))
                {
                    if (_results.TryGetValue(receipt.TransactionId, out ReceiptVerificationResult? verified))
                    {
                        return new ValueTask<ReceiptVerificationResult>(verified);
                    }

                    var verifiedResult = new ReceiptVerificationResult(ReceiptVerificationStatus.Verified, receipt.TransactionId);
                    _results[receipt.TransactionId] = verifiedResult;
                    return new ValueTask<ReceiptVerificationResult>(verifiedResult);
                }

                var result = new ReceiptVerificationResult(
                    ReceiptVerificationStatus.Rejected,
                    receipt.TransactionId,
                    _trustedReceipts.ContainsKey(receipt.TransactionId)
                        ? "交易标识已绑定到不同收据。"
                        : "模拟验证器不信任此收据。");
                return new ValueTask<ReceiptVerificationResult>(result);
            }
        }

        private static bool SameReceipt(StoreReceipt left, StoreReceipt right) =>
            string.Equals(left.TransactionId, right.TransactionId, StringComparison.Ordinal)
            && string.Equals(left.ProductId, right.ProductId, StringComparison.Ordinal)
            && string.Equals(left.Platform, right.Platform, StringComparison.Ordinal)
            && string.Equals(left.Payload, right.Payload, StringComparison.Ordinal);
    }
}
