#if ZOO_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using ZooDefense.Core;
using ZooDefense.Game;
// Both namespaces define these names: say which one is meant.
using IStoreService = ZooDefense.Game.IStoreService;
using Order = UnityEngine.Purchasing.Order;

namespace ZooDefense.Monetization
{
    /// <summary>
    /// Real purchases through Unity IAP 5 (App Store / Google Play). Every product is a consumable; one-time and
    /// weekly limits are enforced by the game. Purchase flow: store charges → OnPurchasePending → the game grants
    /// and saves (Purchases.Fulfill, de-duplicated by transaction id) → ConfirmPurchase. A purchase interrupted
    /// before the confirm is re-delivered by the store at the next launch and granted then.
    /// </summary>
    public sealed class UnityIapStore : MonoBehaviour, IStoreService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            var go = new GameObject("UnityIapStore");
            DontDestroyOnLoad(go);
            Services.Store = go.AddComponent<UnityIapStore>();
        }

        StoreController _store;
        bool _connected, _productsReady;
        readonly Dictionary<string, Action<List<Reward>>> _waiting = new Dictionary<string, Action<List<Reward>>>();
        /// <summary>Delivered before the game finished loading: granted once GameApp is ready.</summary>
        readonly List<PendingOrder> _early = new List<PendingOrder>();

        public bool Ready { get { return _connected && _productsReady; } }

        async void Awake()
        {
            _store = UnityIAPServices.StoreController();

            // Subscribe to everything BEFORE Connect: unfinished purchases may arrive immediately.
            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailed;
            _store.OnPurchaseDeferred += OnPurchaseDeferred;
            _store.OnStoreConnected += OnStoreConnected;
            _store.OnStoreDisconnected += OnStoreDisconnected;
            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += OnProductsFetchFailed;
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

            try { await _store.Connect(); }
            catch (Exception e) { Debug.LogWarning("[Store] connect: " + e.Message); }
        }

        void OnStoreConnected()
        {
            _connected = true;
            var defs = new List<ProductDefinition>();
            foreach (var id in ShopCatalog.RealMoneyProducts(DefaultMeta.Create())) defs.Add(new ProductDefinition(id, ProductType.Consumable));
            _store.FetchProducts(defs);
            _store.FetchPurchases();
        }

        void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _connected = false;
            Debug.LogWarning("[Store] disconnected: " + failure.Message);
        }

        void OnProductsFetched(List<Product> products)
        {
            _productsReady = true;
            Debug.Log("[Store] " + products.Count + " products loaded");
        }

        void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning("[Store] product fetch failed: " + failure.FailureReason);
            _productsReady = _store.GetProducts().Count > 0;
        }

        void OnPurchasesFetched(Orders orders) { }

        void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning("[Store] purchase fetch failed: " + failure.Message);
        }

        // ------------------------------------------------------------------ IStoreService

        public string Price(string productId, string fallback)
        {
            if (_store == null || !_productsReady) return fallback;
            var p = _store.GetProductById(productId);
            return p != null && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString) ? p.metadata.localizedPriceString : fallback;
        }

        public void Purchase(string productId, Action<List<Reward>> onDone)
        {
            var product = _store != null && _productsReady ? _store.GetProductById(productId) : null;
            if (product == null || !product.availableToPurchase)
            {
                Debug.LogWarning("[Store] product not available: " + productId);
                if (onDone != null) onDone(null);
                return;
            }
            _waiting[productId] = onDone;
            _store.PurchaseProduct(product);
        }

        public void Restore(Action<bool> onDone)
        {
            if (_store == null || !_connected) { if (onDone != null) onDone(false); return; }
            _store.RestoreTransactions((ok, error) =>
            {
                if (!ok) Debug.LogWarning("[Store] restore failed: " + error);
                if (onDone != null) onDone(ok);
            });
        }

        // ------------------------------------------------------------------ purchase events

        static string ProductOf(Order order)
        {
            var item = order != null && order.CartOrdered != null ? order.CartOrdered.Items().FirstOrDefault() : null;
            return item != null && item.Product != null ? item.Product.definition.id : null;
        }

        void OnPurchasePending(PendingOrder order)
        {
            if (GameApp.Instance == null || GameApp.Instance.Meta == null) { _early.Add(order); return; }
            Grant(order);
        }

        /// <summary>Orders the server could not check yet (offline): retried every RetryEvery seconds.</summary>
        readonly List<PendingOrder> _retry = new List<PendingOrder>();
        readonly HashSet<string> _checking = new HashSet<string>();
        float _retryTimer;
        const float RetryEvery = 30f;

        void Update()
        {
            if (_early.Count > 0 && GameApp.Instance != null && GameApp.Instance.Meta != null)
            {
                var list = new List<PendingOrder>(_early);
                _early.Clear();
                foreach (var o in list) Grant(o);
            }
            if (_retry.Count > 0 && (_retryTimer += Time.unscaledDeltaTime) >= RetryEvery)
            {
                _retryTimer = 0f;
                var list = new List<PendingOrder>(_retry);
                _retry.Clear();
                foreach (var o in list) Grant(o);
            }
        }

        /// <summary>Server check first (when enabled), then grant + confirm.</summary>
        void Grant(PendingOrder order)
        {
            var id = ProductOf(order);
            var tx = order.Info != null ? order.Info.TransactionID : null;
            var validator = ReceiptValidation.Service;
            if (!MonetizationConfig.Load().serverValidation || validator == null || Application.isEditor)
            {
                FinishGrant(order, id, tx);
                return;
            }
            var key = tx ?? id;
            if (key == null || !_checking.Add(key)) return;
            string store = Application.platform == RuntimePlatform.Android ? "google" : "apple";
            string jws = order.Info != null && order.Info.Apple != null ? order.Info.Apple.jwsRepresentation : null;
            string receipt = order.Info != null ? order.Info.Receipt : null;
            validator.Validate(store, id, tx, jws, receipt, (verdict, reason) =>
            {
                _checking.Remove(key);
                switch (verdict)
                {
                    case ReceiptVerdict.Valid:
                        FinishGrant(order, id, tx);
                        break;
                    case ReceiptVerdict.Invalid:
                        // Never granted and never finished: if the check was wrong, fixing the server delivers it later.
                        Debug.LogWarning("[Store] purchase rejected by the server: " + id + " " + tx + " (" + reason + ")");
                        Answer(id, null);
                        if (GameApp.Instance != null) GameApp.Instance.Toast(Loc.T("shop.verify_failed"));
                        break;
                    default:
                        // Offline / server busy: the store keeps the purchase; try again shortly.
                        _retry.Add(order);
                        Answer(id, null);
                        if (GameApp.Instance != null) GameApp.Instance.Toast(Loc.T("shop.verify_later"));
                        break;
                }
            });
        }

        void FinishGrant(PendingOrder order, string id, string tx)
        {
            var rewards = id != null ? Purchases.Fulfill(id, tx) : null;
            if (rewards == null)
            {
                // Unknown product or the game could not grant: leave it unconfirmed, the store delivers it again.
                Debug.LogError("[Store] could not grant " + id);
                Answer(id, null);
                return;
            }
            // Granted and saved: now finish the transaction.
            _store.ConfirmPurchase(order);
            if (!Answer(id, rewards)) Purchases.RaiseDelivered(id, rewards);
        }

        bool Answer(string id, List<Reward> rewards)
        {
            Action<List<Reward>> done;
            if (id == null || !_waiting.TryGetValue(id, out done)) return false;
            _waiting.Remove(id);
            if (done != null) done(rewards);
            return true;
        }

        void OnPurchaseConfirmed(Order order)
        {
            var failed = order as FailedOrder;
            if (failed != null) Debug.LogWarning("[Store] confirm failed: " + failed.FailureReason + " " + failed.Details);
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            Debug.LogWarning("[Store] purchase failed: " + order.FailureReason + " " + order.Details);
            Answer(ProductOf(order), null);
        }

        void OnPurchaseDeferred(DeferredOrder order)
        {
            // Ask-to-Buy / pending payment: nothing granted until the store sends the purchase.
            var id = ProductOf(order);
            Answer(id, null);
            if (GameApp.Instance != null) GameApp.Instance.Toast(Loc.T("shop.deferred"));
        }

        void OnDestroy()
        {
            if (_store == null) return;
            _store.OnPurchasePending -= OnPurchasePending;
            _store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            _store.OnPurchaseFailed -= OnPurchaseFailed;
            _store.OnPurchaseDeferred -= OnPurchaseDeferred;
            _store.OnStoreConnected -= OnStoreConnected;
            _store.OnStoreDisconnected -= OnStoreDisconnected;
            _store.OnProductsFetched -= OnProductsFetched;
            _store.OnProductsFetchFailed -= OnProductsFetchFailed;
            _store.OnPurchasesFetched -= OnPurchasesFetched;
            _store.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        }
    }
}
#endif
