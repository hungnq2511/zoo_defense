using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;
using ZooDefense.Game;
#if ZOO_CLOUDSAVE
using Unity.Services.CloudSave;
#endif

namespace ZooDefense.Online
{
    /// <summary>
    /// Guest account + Unity Player Accounts link (e-mail / Google / Apple on Unity's sign-in page) + cloud backup
    /// of the profile (Cloud Save). See IAccountService.
    /// </summary>
    public sealed class UgsAccount : IAccountService
    {
        const string LabelKey = "zoo_account_label";
        const string BackupKey = "zoo_account_backup";
        const string SaveKey = "profile";
        /// <summary>Batch backups: at most one upload every this many seconds while the profile keeps changing.</summary>
        const float BackupEvery = 20f;
        /// <summary>Waiting for the player on the sign-in page.</summary>
        const int SignInTimeoutMs = 240000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { Accounts.Service = new UgsAccount(); }

        bool _busy, _linked, _dirty, _uploading;
        float _sinceBackup = BackupEvery;
        TaskCompletionSource<bool> _waitingSignIn;

        public bool Ready { get { return UgsCore.SignedIn; } }
        public bool Busy { get { return _busy; } }
        public bool SigningIn { get { return _waitingSignIn != null; } }
        public bool IsLinked { get { return _linked; } }
        public string LinkedLabel { get { return _linked ? PlayerPrefs.GetString(LabelKey, "Unity ID") : null; } }
        public string PlayerId { get { return Ready ? AuthenticationService.Instance.PlayerId : null; } }
        public long LastBackup { get { long v; return long.TryParse(PlayerPrefs.GetString(BackupKey, "0"), out v) ? v : 0; } }

        // ------------------------------------------------------------------ public operations

        public void Refresh(Action<AccountResult> done)
        {
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
                await ReadLinkStatus();
                return AccountResult.Ok;
            }, done);
        }

        public void Link(Action<AccountResult> done)
        {
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
                var token = await PlayerAccountTokenAsync();
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token);
                }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    return AccountResult.AlreadyLinkedElsewhere;
                }
                RememberLabel();
                _linked = true;
                await Upload();
                return AccountResult.Ok;
            }, done);
        }

        public void SignInExisting(Action<AccountResult, string> done)
        {
            string save = null;
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
                var token = await PlayerAccountTokenAsync();
                var auth = AuthenticationService.Instance;
                // Leave the guest profile of this phone and enter the linked account instead.
                auth.SignOut(true);
                await auth.SignInWithUnityAsync(token);
                RememberLabel();
                _linked = true;
                save = await Download();
                Debug.Log("[Account] signed in to the linked account " + auth.PlayerId + (save != null ? " (cloud save found)" : " (no cloud save)"));
                return AccountResult.Ok;
            }, r => { if (done != null) done(r, r == AccountResult.Ok ? save : null); });
        }

        public void Unlink(Action<AccountResult> done)
        {
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
                await AuthenticationService.Instance.UnlinkUnityAsync();
                PlayerAccountService.Instance.SignOut();
                _linked = false;
                PlayerPrefs.DeleteKey(LabelKey);
                return AccountResult.Ok;
            }, done);
        }

        public void DeleteAccount(Action<AccountResult> done)
        {
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
#if ZOO_CLOUDSAVE
                try { await CloudSaveService.Instance.Data.Player.DeleteAsync(SaveKey, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()); } catch (Exception e) { Debug.LogWarning("[Account] delete save: " + e.Message); }
#endif
                await AuthenticationService.Instance.DeleteAccountAsync();
                AuthenticationService.Instance.SignOut(true);
                if (PlayerAccountService.Instance.IsSignedIn) PlayerAccountService.Instance.SignOut();
                _linked = false;
                PlayerPrefs.DeleteKey(LabelKey);
                PlayerPrefs.DeleteKey(BackupKey);
                return AccountResult.Ok;
            }, done);
        }

        public void BackupNow(Action<AccountResult> done)
        {
            Run(async () =>
            {
                await UgsCore.EnsureSignedInAsync();
                await Upload();
                return AccountResult.Ok;
            }, done);
        }

        /// <summary>Gives up waiting for the sign-in page (player closed the browser).</summary>
        public void CancelSignIn()
        {
            if (_waitingSignIn != null) _waitingSignIn.TrySetCanceled();
        }

        public void MarkDirty() { _dirty = true; }

        public void Update(float dt)
        {
            _sinceBackup += dt;
            if (_dirty && _linked && !_busy && !_uploading && _sinceBackup >= BackupEvery && Ready)
            {
                _dirty = false;
                _sinceBackup = 0f;
                Background(Upload());
            }
        }

        // ------------------------------------------------------------------ internals

        async Task ReadLinkStatus()
        {
            try
            {
                var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
                _linked = info != null && !string.IsNullOrEmpty(info.GetUnityId());
            }
            catch (Exception e) { Debug.LogWarning("[Account] player info: " + e.Message); }
        }

        void RememberLabel()
        {
            string label = null;
            try
            {
                var claims = PlayerAccountService.Instance.IdTokenClaims;
                if (claims != null && !string.IsNullOrEmpty(claims.Email)) label = claims.Email;
            }
            catch (Exception) { }
            PlayerPrefs.SetString(LabelKey, label ?? "Unity ID");
            PlayerPrefs.Save();
        }

        /// <summary>Opens Unity's sign-in page (browser) and waits until the player comes back signed in.</summary>
        async Task<string> PlayerAccountTokenAsync()
        {
            var pa = PlayerAccountService.Instance;
            if (pa.IsSignedIn && !string.IsNullOrEmpty(pa.AccessToken))
            {
                try { await pa.RefreshTokenAsync(); return pa.AccessToken; }
                catch (Exception) { pa.SignOut(); }
            }
            var wait = new TaskCompletionSource<bool>();
            _waitingSignIn = wait;
            Action ok = () => wait.TrySetResult(true);
            Action<RequestFailedException> fail = e => wait.TrySetException(e);
            pa.SignedIn += ok;
            pa.SignInFailed += fail;
            try
            {
                await pa.StartSignInAsync();
                var first = await Task.WhenAny(wait.Task, Task.Delay(SignInTimeoutMs));
                if (first != wait.Task) throw new OperationCanceledException();
                await wait.Task;
            }
            finally
            {
                pa.SignedIn -= ok;
                pa.SignInFailed -= fail;
                _waitingSignIn = null;
            }
            return pa.AccessToken;
        }

        async Task Upload()
        {
#if ZOO_CLOUDSAVE
            var json = Accounts.Snapshot();
            if (string.IsNullOrEmpty(json)) return;
            _uploading = true;
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { SaveKey, json }, { "savedAt", now } });
                PlayerPrefs.SetString(BackupKey, now.ToString());
                Debug.Log("[Account] backed up " + json.Length + " bytes");
            }
            finally { _uploading = false; }
#else
            await Task.CompletedTask;
#endif
        }

        async Task<string> Download()
        {
#if ZOO_CLOUDSAVE
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey });
            Unity.Services.CloudSave.Models.Item item;
            if (data != null && data.TryGetValue(SaveKey, out item) && item != null) return item.Value.GetAs<string>();
            return null;
#else
            await Task.CompletedTask;
            return null;
#endif
        }

        async void Background(Task t)
        {
            try { await t; }
            catch (Exception e) { _dirty = true; Debug.LogWarning("[Account] backup failed: " + e.Message); }
        }

        async void Run(Func<Task<AccountResult>> work, Action<AccountResult> done)
        {
            if (_busy) { if (done != null) done(AccountResult.Error); return; }
            _busy = true;
            AccountResult result;
            try { result = await work(); }
            catch (OperationCanceledException) { result = AccountResult.Cancelled; }
            catch (PlayerAccountsException e)
            {
                Debug.LogWarning("[Account] player accounts: " + e.ErrorCode + " " + e.Message);
                result = e.ErrorCode == PlayerAccountsErrorCodes.MissingClientId || e.ErrorCode == PlayerAccountsErrorCodes.InvalidClient ? AccountResult.NotConfigured : AccountResult.Error;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning("[Account] " + e.GetType().Name + " " + e.ErrorCode + ": " + e.Message);
                result = e.ErrorCode == CommonErrorCodes.TransportError || e.ErrorCode == CommonErrorCodes.Timeout ? AccountResult.Offline : AccountResult.Error;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] " + e.GetType().Name + ": " + e.Message);
                result = AccountResult.Error;
            }
            _busy = false;
            if (done != null) done(result);
        }
    }
}
